SCRIPT_DIR="$(cd "$(dirname "$0")" &> /dev/null && pwd)"

for fullname in "$SCRIPT_DIR/../testdata/backupdest"/duplicati-*.dlist.zip.aes; do
    if [ ! -e "$fullname" ]; then
        echo "No dlist files found in testdata/backupdest"
        exit 1
    fi
    name=${fullname##*/}
    echo "Processing $name"
    bash "$SCRIPT_DIR/inject-dlist.sh" "$name"
done