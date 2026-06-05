#!/usr/bin/env bash
set -euo pipefail

target_dir="${1:-Heroes3MapReader.Logic/LanguageDictionaries}"
base_url="https://raw.githubusercontent.com/LibreOffice/dictionaries/master"
mkdir -p "$target_dir"

download_dictionary() {
    local source_path="$1"
    local output_name="$2"
    local output_path="$target_dir/$output_name"

    echo "Downloading $output_name from LibreOffice dictionaries..."
    curl --fail --location --retry 3 --silent --show-error \
        --output "$output_path.tmp" \
        "$base_url/$source_path"
    mv "$output_path.tmp" "$output_path"
}

download_dictionary "cs_CZ/cs_CZ.dic" "cs_CZ.dic"
download_dictionary "en/en_US.dic" "en_US.dic"
download_dictionary "pl_PL/pl_PL.dic" "pl_PL.dic"
download_dictionary "de/de_DE_frami.dic" "de_DE.dic"
download_dictionary "fr_FR/fr.dic" "fr_FR.dic"
download_dictionary "hu_HU/hu_HU.dic" "hu_HU.dic"
download_dictionary "sv_SE/sv_SE.dic" "sv_SE.dic"
download_dictionary "es/es_ES.dic" "es_ES.dic"
download_dictionary "it_IT/it_IT.dic" "it_IT.dic"

cat > "$target_dir/SOURCES.md" <<'SOURCES'
# Public dictionary sources

The `.dic` files in this directory are downloaded from the public LibreOffice dictionaries repository during CI/publish builds:

https://github.com/LibreOffice/dictionaries

Each dictionary remains under its upstream license. See the matching upstream directory in the LibreOffice dictionaries repository for authorship and license details.
SOURCES
