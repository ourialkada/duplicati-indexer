#!/bin/bash

# Exit on error
set -e

echo "========================================"
echo "    Fetching Enron Email Dataset"
echo "========================================"

# Navigate to testdata directory relative to script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &> /dev/null && pwd)"
cd "$SCRIPT_DIR/testdata"

DATASET_URL="https://www.cs.cmu.edu/~enron/enron_mail_20150507.tar.gz"
ARCHIVE_NAME="enron_mail_20150507.tar.gz"

if [ ! -f "$ARCHIVE_NAME" ]; then
    echo "Downloading Enron Email Dataset (~423MB)..."
    curl -L -O "$DATASET_URL"
else
    echo "Archive '$ARCHIVE_NAME' already exists. Skipping download."
fi

if [ ! -d "source" ]; then
    echo "Extracting archive..."
    tar -xzf "$ARCHIVE_NAME"
    
    # The archive extracts to a folder named 'maildir' by default
    # The backup testing scripts require this to be named 'source'
    if [ -d "maildir" ]; then
        mv maildir source
        echo "Successfully extracted and organized into 'testdata/source/'"
    else
        echo "Error: Extraction did not create expected 'maildir' folder."
        exit 1
    fi
else
    echo "The 'source' directory already exists. Skipping extraction."
fi

echo ""
echo "Test data fetch complete!"
echo "Next steps: cd into 'testdata' and run:"
echo "  1) python get_week_data.py"
echo "  2) python backup_weeks.py"
