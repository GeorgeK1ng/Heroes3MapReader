#!/usr/bin/env bash
set -euo pipefail

target_dir="${1:-Heroes3MapReader.Logic/LanguageDictionaries}"
archive_url="https://codeload.github.com/LibreOffice/dictionaries/tar.gz/refs/heads/master"
temp_dir="$(mktemp -d)"
archive_path="$temp_dir/libreoffice-dictionaries.tar.gz"
trap 'rm -rf "$temp_dir"' EXIT

mkdir -p "$target_dir"
rm -f "$target_dir"/*.dic

echo "Downloading all available LibreOffice Hunspell dictionaries..."
curl --fail --location --retry 3 --silent --show-error \
    --output "$archive_path" \
    "$archive_url"

downloaded_count=0
while IFS= read -r dictionary_path; do
    file_name="${dictionary_path##*/}"
    lower_file_name="${file_name,,}"

    case "$lower_file_name" in
        hyph_*|*hyph*.dic)
            continue
            ;;
    esac

    output_name="${file_name//-/_}"
    output_path="$target_dir/$output_name"
    if [[ -e "$output_path" ]]; then
        echo "Skipping duplicate dictionary file name: $output_name ($dictionary_path)"
        continue
    fi

    tar -xOf "$archive_path" "$dictionary_path" > "$output_path.tmp"
    mv "$output_path.tmp" "$output_path"
    downloaded_count=$((downloaded_count + 1))
done < <(tar -tzf "$archive_path" | while IFS= read -r archive_entry; do
    case "$archive_entry" in
        *.dic)
            printf '%s\n' "$archive_entry"
            ;;
    esac
done)

cat > "$target_dir/SOURCES.md" <<'SOURCES'
# Public dictionary sources

The `.dic` files in this directory are downloaded from the public LibreOffice dictionaries repository during CI/publish builds:

https://github.com/LibreOffice/dictionaries

Each dictionary remains under its upstream license. See the matching upstream directory in the LibreOffice dictionaries repository for authorship and license details.
SOURCES

echo "Downloaded $downloaded_count LibreOffice dictionary files into $target_dir."
