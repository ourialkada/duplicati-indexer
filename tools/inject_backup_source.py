#!/usr/bin/env python3
"""
Tool to inject BackupSource records into PostgreSQL for DuplicatiIndexer.

This script inserts BackupSource documents directly into the PostgreSQL database
using Marten's document format. It connects to the database and inserts records
into the mt_doc_backupsource table.
"""

import argparse
import os
import sys
import uuid
from datetime import datetime, timezone
from typing import Any, Callable, Optional
import psycopg2
from psycopg2.extras import Json


def parse_connection_string(connection_string: str) -> dict:
    """
    Parse a PostgreSQL connection string into its components.
    
    Expected format: Host=postgres;Port=5432;Database=duplicati_indexer;Username=postgres;Password=postgres
    
    Args:
        connection_string: The connection string to parse
        
    Returns:
        Dict with host, port, database, username, password keys
    """
    config = {}
    
    # Parse each key-value pair separated by semicolons
    pairs = connection_string.split(';')
    for pair in pairs:
        pair = pair.strip()
        if not pair:
            continue
        if '=' in pair:
            key, value = pair.split('=', 1)
            key = key.strip().lower()
            value = value.strip()
            
            if key in ('host', 'server'):
                config['host'] = value
            elif key == 'port':
                config['port'] = int(value)
            elif key in ('database', 'db'):
                config['database'] = value
            elif key in ('username', 'user', 'userid', 'user id'):
                config['username'] = value
            elif key in ('password', 'pass'):
                config['password'] = value
    
    return config


def resolve_config(config, sources, key, env_var, arg_value, default_value, arg_name, type_func: Callable[[str], Any] = str):
    """
    Resolves configuration value with priority:
    1. Already in config (from connection string)
    2. Environment variable
    3. CLI argument (if different from default)
    4. Default value
    """
    if env_var:
        env_value = os.environ.get(env_var)
        if env_value:
            config[key] = type_func(env_value)
            sources[key] = f'environment variable ({env_var})'
            return
    if key not in config:
        if arg_value != default_value:
            config[key] = arg_value
            sources[key] = f'command-line argument ({arg_name})'
        else:
            config[key] = default_value
            sources[key] = 'default'


def get_config_from_env_or_args(args) -> tuple[dict, dict]:
    """
    Get configuration from environment variables or command-line arguments.
    Priority: ConnectionStrings__DocumentStore > Individual env vars > CLI arguments > Defaults

    Returns a dict with the connection parameters and a dict indicating sources.
    """
    config = {}
    sources = {}

    # First, try to parse from ConnectionStrings__DocumentStore
    connection_string = os.environ.get('ConnectionStrings__DocumentStore')
    if connection_string:
        try:
            parsed = parse_connection_string(connection_string)
            config.update(parsed)
            for key in parsed:
                sources[key] = 'environment variable (ConnectionStrings__DocumentStore)'
        except Exception as e:
            print(f"Warning: Failed to parse ConnectionStrings__DocumentStore: {e}")
    
    # PostgreSQL Host
    resolve_config(config, sources, 'host', 'POSTGRES_HOST', args.host, 'localhost', '--host')
    
    # PostgreSQL Port
    resolve_config(config, sources, 'port', 'POSTGRES_PORT', args.port, 5432, '--port', type_func=int)
    
    # PostgreSQL Database
    resolve_config(config, sources, 'database', 'POSTGRES_DB', args.database, 'duplicati_indexer', '--database')
    
    # PostgreSQL Username
    resolve_config(config, sources, 'username', 'POSTGRES_USER', args.username, 'postgres', '--username')
    
    # PostgreSQL Password
    resolve_config(config, sources, 'password', 'POSTGRES_PASSWORD', args.password, 'postgres', '--password')
    
    return config, sources


class BackupSourceInjector:
    """Handles connection to PostgreSQL and BackupSource document insertion."""

    def __init__(
        self,
        host: str = "localhost",
        port: int = 5432,
        database: str = "duplicati_indexer",
        username: str = "postgres",
        password: str = "postgres",
    ):
        self.host = host
        self.port = port
        self.database = database
        self.username = username
        self.password = password
        self.connection: Optional['psycopg2.extensions.connection'] = None

    def connect(self) -> bool:
        """Establish connection to PostgreSQL server."""

        try:
            self.connection = psycopg2.connect(
                host=self.host,
                port=self.port,
                database=self.database,
                user=self.username,
                password=self.password,
            )
            print(f"Connected to PostgreSQL at {self.host}:{self.port}/{self.database}")
            return True
        except psycopg2.OperationalError as e:
            print(f"Error: Failed to connect to PostgreSQL at {self.host}:{self.port}/{self.database}")
            print(f"Details: {e}")
            return False
        except Exception as e:
            print(f"Error: Unexpected error connecting to PostgreSQL: {e}")
            return False

    def disconnect(self) -> None:
        """Close the PostgreSQL connection."""
        if self.connection:
            self.connection.close()
            print("Disconnected from PostgreSQL")

    def insert_backup_source(
        self,
        name: str,
        duplicati_backup_id: str,
        source_id: Optional[str] = None,
        created_at: Optional[datetime] = None,
        last_parsed_version: Optional[str] = None,
        encryption_password: Optional[str] = None,
        target_url: Optional[str] = None,
    ) -> bool:
        """
        Insert a BackupSource document into PostgreSQL.

        Args:
            name: The display name of the backup source
            duplicati_backup_id: The Duplicati backup identifier
            source_id: Optional GUID for the backup source (auto-generated if not provided)
            created_at: Optional creation timestamp (current time if not provided)
            last_parsed_version: Optional ISO 8601 timestamp of last parsed version
            encryption_password: Optional encryption passphrase for the backup
            target_url: Optional target URL (connection string) for backup storage

        Returns:
            True if document was inserted successfully, False otherwise
        """
        if not self.connection:
            print("Error: Not connected to PostgreSQL. Call connect() first.")
            return False

        try:
            # Generate UUID if not provided
            doc_id = uuid.UUID(source_id) if source_id else uuid.uuid4()

            # Use current time if not provided
            if created_at is None:
                created_at = datetime.now(timezone.utc)

            # Build the document data matching BackupSource entity structure
            document: dict = {
                "Id": str(doc_id),
                "Name": name,
                "DuplicatiBackupId": duplicati_backup_id,
                "CreatedAt": created_at.isoformat(),
            }

            # Add optional fields only if provided
            if last_parsed_version:
                document["LastParsedVersion"] = last_parsed_version
            if encryption_password:
                document["EncryptionPassword"] = encryption_password
            if target_url:
                document["TargetUrl"] = target_url
            
            cursor = self.connection.cursor()
            
            # Insert into Marten's document table
            # Marten stores documents in mt_doc_<lowercase-type-name> table
            # with id and data (JSONB) columns
            query = """
                INSERT INTO mt_doc_backupsource (id, data)
                VALUES (%s, %s)
                ON CONFLICT (id) DO UPDATE SET
                    data = EXCLUDED.data
                RETURNING id
            """
            
            cursor.execute(query, (str(doc_id), Json(document)))
            result = cursor.fetchone()
            self.connection.commit()
            cursor.close()
            
            if result:
                print(f"Successfully inserted BackupSource with ID: {result[0]}")
                print(f"  Name: {name}")
                print(f"  DuplicatiBackupId: {duplicati_backup_id}")
                return True
            else:
                print("Error: Failed to insert BackupSource document")
                return False
                
        except psycopg2.Error as e:
            print(f"Error: Database error while inserting BackupSource: {e}")
            if self.connection:
                self.connection.rollback()
            return False
        except ValueError as e:
            print(f"Error: Invalid UUID format for source-id: {e}")
            return False
        except Exception as e:
            print(f"Error: Unexpected error while inserting BackupSource: {e}")
            if self.connection:
                self.connection.rollback()
            return False


def validate_positive_int(value: str) -> int:
    """Validate that a string is a positive integer."""
    try:
        ivalue = int(value)
        if ivalue < 0:
            raise argparse.ArgumentTypeError(f"Value must be non-negative, got: {value}")
        return ivalue
    except ValueError:
        raise argparse.ArgumentTypeError(f"Invalid integer value: {value}")


def main():
    parser = argparse.ArgumentParser(
        description="Inject BackupSource records into PostgreSQL for DuplicatiIndexer",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Environment Variables (in order of priority):
  ConnectionStrings__DocumentStore      Full connection string for Marten document store (highest priority)
    Format: Host=postgres;Port=5432;Database=duplicati_indexer;Username=postgres;Password=postgres
  POSTGRES_HOST                         PostgreSQL server hostname
  POSTGRES_PORT                         PostgreSQL server port
  POSTGRES_DB                           PostgreSQL database name
  POSTGRES_USER                         PostgreSQL username
  POSTGRES_PASSWORD                     PostgreSQL password

Note: This tool connects to the PostgreSQL database where Marten stores documents.
Wolverine (message bus) shares the same database but uses different tables.

Examples:
  # Basic usage with auto-generated ID
  python inject_backup_source.py --name "My Backup" --duplicati-backup-id "backup-123"

  # With encryption password and target URL
  python inject_backup_source.py \\
      --name "Production Backup" \\
      --duplicati-backup-id "prod-backup-001" \\
      --encryption-password "my-secret-passphrase" \\
      --target-url "file:///backups/production"

  # With specific ID, creation time, and last parsed version
  python inject_backup_source.py \\
      --name "Production Backup" \\
      --duplicati-backup-id "prod-backup-001" \\
      --source-id "550e8400-e29b-41d4-a716-446655440000" \\
      --created-at "2024-01-15T10:30:00Z" \\
      --last-parsed-version "2024-01-15T10:30:00Z" \\
      --encryption-password "my-secret" \\
      --target-url "s3://mybucket/backups"

  # Using ConnectionStrings__DocumentStore environment variable
  export ConnectionStrings__DocumentStore="Host=postgres;Port=5432;Database=duplicati_indexer;Username=postgres;Password=secret"
  python inject_backup_source.py --name "Test Backup" --duplicati-backup-id "test-123"

  # Using individual environment variables
  export POSTGRES_HOST="db.example.com"
  export POSTGRES_USER="admin"
  export POSTGRES_PASSWORD="secret"
  python inject_backup_source.py --name "Test Backup" --duplicati-backup-id "test-123"

  # With custom database connection via CLI arguments
  python inject_backup_source.py \\
      --host "db.example.com" \\
      --port 5432 \\
      --database "mydb" \\
      --username "admin" \\
      --password "secret" \\
      --name "Test Backup" \\
      --duplicati-backup-id "test-123" \\
      --target-url "file:///backups"
        """
    )

    # BackupSource parameters
    parser.add_argument(
        "--name",
        required=True,
        help="The display name of the backup source (required)",
    )
    parser.add_argument(
        "--duplicati-backup-id",
        required=True,
        help="The Duplicati backup identifier (required)",
    )
    parser.add_argument(
        "--source-id",
        help="Optional GUID for the backup source (auto-generated if not provided)",
    )
    parser.add_argument(
        "--created-at",
        help="Optional creation timestamp in ISO 8601 format (current time if not provided)",
    )
    parser.add_argument(
        "--last-parsed-version",
        help="Optional ISO 8601 timestamp of last parsed version (e.g., '2024-03-12T14:30:00Z')",
    )
    parser.add_argument(
        "--encryption-password",
        default=None,
        help="Encryption passphrase for the backup (optional)",
    )
    parser.add_argument(
        "--target-url",
        default=None,
        help="Target URL (connection string) for backup storage (e.g., 'file:///backups' or 's3://bucket/path')",
    )

    # PostgreSQL connection parameters
    parser.add_argument(
        "--host",
        default="localhost",
        help="PostgreSQL server hostname (default: localhost)",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=5432,
        help="PostgreSQL server port (default: 5432)",
    )
    parser.add_argument(
        "--database",
        default="duplicati_indexer",
        help="PostgreSQL database name (default: duplicati_indexer)",
    )
    parser.add_argument(
        "--username",
        default="postgres",
        help="PostgreSQL username (default: postgres)",
    )
    parser.add_argument(
        "--password",
        default="postgres",
        help="PostgreSQL password (default: postgres)",
    )

    args = parser.parse_args()

    # Parse created_at if provided
    created_at = None
    if args.created_at:
        try:
            # Try parsing ISO 8601 format
            created_at = datetime.fromisoformat(args.created_at.replace('Z', '+00:00'))
        except ValueError as e:
            print(f"Error: Invalid timestamp format for --created-at: {e}")
            print("Expected format: ISO 8601 (e.g., '2024-01-15T10:30:00Z')")
            sys.exit(1)

    # Get configuration from environment variables or CLI arguments
    config, sources = get_config_from_env_or_args(args)
    
    # Print configuration sources for debugging
    print("Configuration sources:")
    print(f"  Host:     {config['host']} (from {sources['host']})")
    print(f"  Port:     {config['port']} (from {sources['port']})")
    print(f"  Database: {config['database']} (from {sources['database']})")
    print(f"  Username: {config['username']} (from {sources['username']})")
    print(f"  Password: {'*' * len(config['password'])} (from {sources['password']})")
    print()

    # Create injector and insert backup source
    injector = BackupSourceInjector(
        host=config['host'],
        port=config['port'],
        database=config['database'],
        username=config['username'],
        password=config['password'],
    )

    if not injector.connect():
        sys.exit(1)

    try:
        success = injector.insert_backup_source(
            name=args.name,
            duplicati_backup_id=args.duplicati_backup_id,
            source_id=args.source_id,
            created_at=created_at,
            last_parsed_version=args.last_parsed_version,
            encryption_password=args.encryption_password,
            target_url=args.target_url,
        )
        
        if not success:
            sys.exit(1)
    finally:
        injector.disconnect()


if __name__ == "__main__":
    main()
