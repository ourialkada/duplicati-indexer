#!/usr/bin/env python3
"""
Tool to inject BackupVersionCreated messages into Wolverine PostgreSQL persistence for DuplicatiIndexer.

This script inserts messages directly into the Wolverine incoming_envelopes table,
which triggers dlist file processing in the DuplicatiIndexer service.
"""

import argparse
import json
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
    Priority: ConnectionStrings__MessageStore > Individual env vars > CLI arguments > Defaults

    Returns a dict with the connection parameters and a dict indicating sources.
    """
    config = {}
    sources = {}

    # First, try to parse from ConnectionStrings__MessageStore
    connection_string = os.environ.get('ConnectionStrings__MessageStore')
    if connection_string:
        try:
            parsed = parse_connection_string(connection_string)
            config.update(parsed)
            for key in parsed:
                sources[key] = 'environment variable (ConnectionStrings__MessageStore)'
        except Exception as e:
            print(f"Warning: Failed to parse ConnectionStrings__MessageStore: {e}")
    
    # PostgreSQL Host
    resolve_config(config, sources, 'host', 'POSTGRES_HOST', args.host, 'localhost', '--host')
    
    # PostgreSQL Port
    resolve_config(config, sources, 'port', 'POSTGRES_PORT', args.port, 5432, '--port', type_func=int)
    
    # PostgreSQL Database - defaults to duplicati_indexer (same as Marten)
    resolve_config(config, sources, 'database', 'POSTGRES_DB', args.database, 'duplicati_indexer', '--database')
    
    # PostgreSQL Username
    resolve_config(config, sources, 'username', 'POSTGRES_USER', args.username, 'postgres', '--username')
    
    # PostgreSQL Password
    resolve_config(config, sources, 'password', 'POSTGRES_PASSWORD', args.password, 'postgres', '--password')
    
    # Queue name (kept for backward compatibility but ignored - always uses wolverine.incoming_envelopes)
    resolve_config(config, sources, 'queue', None, args.queue, 'BackupVersionCreated', '--queue')
    
    return config, sources


class WolverinePostgresMessageInjector:
    """Handles connection to PostgreSQL and Wolverine message insertion."""

    def __init__(
        self,
        host: str = "localhost",
        port: int = 5432,
        database: str = "duplicati_indexer",
        username: str = "postgres",
        password: str = "postgres",
        queue: str = "BackupVersionCreated",
    ):
        self.host = host
        self.port = port
        self.database = database
        self.username = username
        self.password = password
        self.queue = queue  # Kept for backward compatibility, but not used
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

    def _ensure_table_exists(self, cursor) -> bool:
        """
        Check if the Wolverine incoming_envelopes table exists, and provide helpful error if not.
        """
        cursor.execute("""
            SELECT EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_schema = 'wolverine' 
                AND table_name = 'incoming_envelopes'
            )
        """)
        
        exists = cursor.fetchone()[0]
        if not exists:
            print("Error: Wolverine table 'wolverine.incoming_envelopes' does not exist.")
            print("Make sure Wolverine has been initialized and the DuplicatiIndexer service has started at least once.")
            return False
        return True

    def publish_message(
        self,
        backup_id: str,
        dlist_filename: str,
    ) -> bool:
        """
        Publish a BackupVersionCreated message to Wolverine PostgreSQL persistence.

        The version timestamp is extracted from the dlist filename
        (format: duplicati-YYYYMMDDTHHMMSSZ.dlist.zip[.aes]).

        Wolverine uses a simpler envelope format than MassTransit. Messages are stored
        in the wolverine.incoming_envelopes table with the following columns:
        - id: Message UUID
        - owner_id: 0 for local processing
        - destination: "local" for local handlers
        - body: JSON payload containing the message data
        - message_type: Full type name of the message
        - attempts: 0 (initial attempt count)
        - scheduled_time: NULL
        - deliver_by: NULL

        Args:
            backup_id: The backup identifier (GUID or string)
            dlist_filename: The dlist filename (e.g., "duplicati-20240312T143000Z.dlist.zip.aes")

        Returns:
            True if message was published successfully, False otherwise
        """

        if not self.connection:
            print("Error: Not connected to PostgreSQL. Call connect() first.")
            return False

        cursor = self.connection.cursor()
        
        # Check if table exists
        if not self._ensure_table_exists(cursor):
            cursor.close()
            return False

        try:
            # Build the message payload matching BackupVersionCreated record structure
            message_payload = {
                "BackupId": backup_id,
                "DlistFilename": dlist_filename,
            }

            # Generate message UUID
            message_id = str(uuid.uuid4())
            
            # Wolverine envelope format - much simpler than MassTransit
            # Insert into wolverine.incoming_envelopes table
            query = """
                INSERT INTO wolverine.incoming_envelopes (
                    id,
                    owner_id,
                    destination,
                    body,
                    message_type,
                    attempts,
                    scheduled_time,
                    deliver_by
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
            """
            
            cursor.execute(query, (
                message_id,                    # id: Message UUID
                0,                             # owner_id: 0 for local processing
                "local",                       # destination: "local" for local handlers
                Json(message_payload),         # body: JSON payload
                "DuplicatiIndexer.Messages.BackupVersionCreated",  # message_type
                0,                             # attempts: initial count
                None,                          # scheduled_time: NULL
                None,                          # deliver_by: NULL
            ))
            
            self.connection.commit()
            cursor.close()
            
            print(f"Published message: BackupId={backup_id}, DlistFilename={dlist_filename}")
            print(f"  Table: wolverine.incoming_envelopes")
            print(f"  MessageId: {message_id}")
            return True
            
        except psycopg2.Error as e:
            print(f"Error: Database error while publishing message: {e}")
            if self.connection:
                self.connection.rollback()
            cursor.close()
            return False
        except Exception as e:
            print(f"Error: Unexpected error while publishing message: {e}")
            if self.connection:
                self.connection.rollback()
            cursor.close()
            return False


def main():
    parser = argparse.ArgumentParser(
        description="Inject BackupVersionCreated messages into Wolverine PostgreSQL persistence for DuplicatiIndexer",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Environment Variables (in order of priority):
  ConnectionStrings__MessageStore       Full connection string for Wolverine messaging (highest priority)
    Format: Host=postgres;Port=5432;Database=duplicati_indexer;Username=postgres;Password=postgres
  POSTGRES_HOST                        PostgreSQL server hostname
  POSTGRES_PORT                        PostgreSQL server port
  POSTGRES_DB                          PostgreSQL database name (default: duplicati_indexer)
  POSTGRES_USER                        PostgreSQL username
  POSTGRES_PASSWORD                    PostgreSQL password

Note: This tool connects directly to PostgreSQL where Wolverine stores messages.
Wolverine shares the same PostgreSQL database as Marten by default.

Legacy Note: The --queue argument is kept for backward compatibility but is ignored.
Wolverine always uses the 'wolverine.incoming_envelopes' table.

Examples:
  # Basic usage
  python inject_dlist_message.py --backup-id "my-backup-123" --dlist-filename "duplicati-20240312T143000Z.dlist.zip.aes"

  # Using ConnectionStrings__MessageStore environment variable (recommended)
  export ConnectionStrings__MessageStore="Host=postgres;Port=5432;Database=duplicati_indexer;Username=postgres;Password=secret"
  python inject_dlist_message.py --backup-id "abc-123" --dlist-filename "duplicati-20240312T143000Z.dlist.zip"

  # Using individual environment variables
  export POSTGRES_HOST="postgres.example.com"
  export POSTGRES_USER="admin"
  export POSTGRES_PASSWORD="secret"
  python inject_dlist_message.py --backup-id "abc-123" --dlist-filename "duplicati-20240312T143000Z.dlist.zip.aes"

  # With custom database connection via CLI arguments
  python inject_dlist_message.py \\
      --host "postgres.example.com" \\
      --port 5432 \\
      --database "duplicati_indexer" \\
      --username "admin" \\
      --password "secret" \\
      --backup-id "abc-123" \\
      --dlist-filename "duplicati-20240312T143000Z.dlist.zip.aes"
        """
    )

    # Message parameters
    parser.add_argument(
        "--backup-id",
        required=True,
        help="The backup identifier (GUID or string)",
    )
    parser.add_argument(
        "--dlist-filename",
        required=True,
        help="The dlist filename (e.g., 'duplicati-20240312T143000Z.dlist.zip.aes'). The version timestamp is extracted from this filename.",
    )

    # PostgreSQL connection parameters
    parser.add_argument(
        "--host",
        default="localhost",
        help="PostgreSQL host (default: localhost)",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=5432,
        help="PostgreSQL port (default: 5432)",
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
    parser.add_argument(
        "--queue",
        default="BackupVersionCreated",
        help="Queue name (legacy, ignored - kept for backward compatibility)",
    )

    args = parser.parse_args()

    # Validate parameters
    has_errors = False

    if not args.dlist_filename.strip():
        print("Error: Dlist filename cannot be empty")
        has_errors = True
    elif not args.dlist_filename.startswith("duplicati-") or ".dlist." not in args.dlist_filename:
        print("Warning: Dlist filename should follow the format: duplicati-YYYYMMDDTHHMMSSZ.dlist.zip[.aes]")

    if has_errors:
        sys.exit(1)

    # Get configuration from environment variables or CLI arguments
    config, sources = get_config_from_env_or_args(args)
    
    # Print configuration sources for debugging
    print("Configuration:")
    print(f"  Host:     {config['host']} (from {sources['host']})")
    print(f"  Port:     {config['port']} (from {sources['port']})")
    print(f"  Database: {config['database']} (from {sources['database']})")
    print(f"  Username: {config['username']} (from {sources['username']})")
    print(f"  Password: {'*' * len(config['password'])} (from {sources['password']})")
    print(f"  Queue:    {config['queue']} (from {sources['queue']}, legacy/ignored)")
    print()

    # Create injector and publish message
    injector = WolverinePostgresMessageInjector(
        host=config['host'],
        port=config['port'],
        database=config['database'],
        username=config['username'],
        password=config['password'],
        queue=config['queue'],
    )

    if not injector.connect():
        sys.exit(1)

    try:
        success = injector.publish_message(
            backup_id=args.backup_id,
            dlist_filename=args.dlist_filename,
        )
        
        if not success:
            sys.exit(1)
    finally:
        injector.disconnect()


if __name__ == "__main__":
    main()
