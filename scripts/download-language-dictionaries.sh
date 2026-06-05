#!/usr/bin/env bash
set -euo pipefail

target_dir="${1:-Heroes3MapReader.Logic/LanguageDictionaries}"
libreoffice_base_url="https://raw.githubusercontent.com/LibreOffice/dictionaries/master"
wooorm_base_url="https://raw.githubusercontent.com/wooorm/dictionaries/main/dictionaries"
mkdir -p "$target_dir"

download_dictionary() {
    local language_name="$1"
    local output_name="$2"
    shift 2

    local output_path="$target_dir/$output_name"
    local temp_path="$output_path.tmp"

    rm -f "$temp_path"
    for source_url in "$@"; do
        echo "Downloading $output_name ($language_name) from $source_url..."
        if curl --fail --location --retry 3 --silent --show-error \
            --output "$temp_path" \
            "$source_url"; then
            mv "$temp_path" "$output_path"
            return 0
        fi

        rm -f "$temp_path"
    done

    echo "::warning::No public Hunspell dictionary was downloaded for $language_name ($output_name)."
}

lo_dictionary_url() {
    echo "$libreoffice_base_url/$1"
}

wooorm_dictionary_url() {
    echo "$wooorm_base_url/$1/index.dic"
}

# Keep this list aligned with the translation languages exposed by VCMI's mod repository.
download_dictionary "Belarusian" "be_BY.dic" \
    "$(lo_dictionary_url 'be_BY/be_BY.dic')" \
    "$(wooorm_dictionary_url 'be')"
download_dictionary "Bulgarian" "bg_BG.dic" \
    "$(lo_dictionary_url 'bg_BG/bg_BG.dic')" \
    "$(wooorm_dictionary_url 'bg')"
download_dictionary "Chinese" "zh_CN.dic" \
    "$(wooorm_dictionary_url 'zh')"
download_dictionary "Czech" "cs_CZ.dic" \
    "$(lo_dictionary_url 'cs_CZ/cs_CZ.dic')" \
    "$(wooorm_dictionary_url 'cs')"
download_dictionary "Dutch" "nl_NL.dic" \
    "$(lo_dictionary_url 'nl_NL/nl_NL.dic')" \
    "$(wooorm_dictionary_url 'nl')"
download_dictionary "English" "en_US.dic" \
    "$(lo_dictionary_url 'en/en_US.dic')" \
    "$(wooorm_dictionary_url 'en')"
download_dictionary "Filipino" "fil_PH.dic" \
    "$(wooorm_dictionary_url 'fil')" \
    "$(wooorm_dictionary_url 'tl')"
download_dictionary "Finnish" "fi_FI.dic" \
    "$(lo_dictionary_url 'fi_FI/fi_FI.dic')" \
    "$(wooorm_dictionary_url 'fi')"
download_dictionary "French" "fr_FR.dic" \
    "$(lo_dictionary_url 'fr_FR/fr.dic')" \
    "$(wooorm_dictionary_url 'fr')"
download_dictionary "German" "de_DE.dic" \
    "$(lo_dictionary_url 'de/de_DE_frami.dic')" \
    "$(wooorm_dictionary_url 'de')"
download_dictionary "Greek" "el_GR.dic" \
    "$(lo_dictionary_url 'el_GR/el_GR.dic')" \
    "$(wooorm_dictionary_url 'el')"
download_dictionary "Hungarian" "hu_HU.dic" \
    "$(lo_dictionary_url 'hu_HU/hu_HU.dic')" \
    "$(wooorm_dictionary_url 'hu')"
download_dictionary "Italian" "it_IT.dic" \
    "$(lo_dictionary_url 'it_IT/it_IT.dic')" \
    "$(wooorm_dictionary_url 'it')"
download_dictionary "Japanese" "ja_JP.dic" \
    "$(wooorm_dictionary_url 'ja')"
download_dictionary "Korean" "ko_KR.dic" \
    "$(wooorm_dictionary_url 'ko')"
download_dictionary "Latvian" "lv_LV.dic" \
    "$(lo_dictionary_url 'lv_LV/lv_LV.dic')" \
    "$(wooorm_dictionary_url 'lv')"
download_dictionary "Norwegian" "nb_NO.dic" \
    "$(lo_dictionary_url 'nb_NO/nb_NO.dic')" \
    "$(wooorm_dictionary_url 'nb')"
download_dictionary "Polish" "pl_PL.dic" \
    "$(lo_dictionary_url 'pl_PL/pl_PL.dic')" \
    "$(wooorm_dictionary_url 'pl')"
download_dictionary "Portuguese (Brazil)" "pt_BR.dic" \
    "$(lo_dictionary_url 'pt_BR/pt_BR.dic')" \
    "$(wooorm_dictionary_url 'pt-br')" \
    "$(wooorm_dictionary_url 'pt')"
download_dictionary "Romanian" "ro_RO.dic" \
    "$(lo_dictionary_url 'ro_RO/ro_RO.dic')" \
    "$(wooorm_dictionary_url 'ro')"
download_dictionary "Russian" "ru_RU.dic" \
    "$(lo_dictionary_url 'ru_RU/ru_RU.dic')" \
    "$(wooorm_dictionary_url 'ru')"
download_dictionary "Serbian" "sr_Latn_RS.dic" \
    "$(lo_dictionary_url 'sr/sr-Latn.dic')" \
    "$(lo_dictionary_url 'sr/sr-Latn-RS.dic')" \
    "$(wooorm_dictionary_url 'sr')"
download_dictionary "Spanish" "es_ES.dic" \
    "$(lo_dictionary_url 'es/es_ES.dic')" \
    "$(wooorm_dictionary_url 'es')"
download_dictionary "Swedish" "sv_SE.dic" \
    "$(lo_dictionary_url 'sv_SE/sv_SE.dic')" \
    "$(wooorm_dictionary_url 'sv')"
download_dictionary "Turkish" "tr_TR.dic" \
    "$(lo_dictionary_url 'tr_TR/tr_TR.dic')" \
    "$(wooorm_dictionary_url 'tr')"
download_dictionary "Ukrainian" "uk_UA.dic" \
    "$(lo_dictionary_url 'uk_UA/uk_UA.dic')" \
    "$(wooorm_dictionary_url 'uk')"
download_dictionary "Vietnamese" "vi_VN.dic" \
    "$(lo_dictionary_url 'vi_VN/vi_VN.dic')" \
    "$(wooorm_dictionary_url 'vi')"

cat > "$target_dir/SOURCES.md" <<'SOURCES'
# Public dictionary sources

The `.dic` files in this directory are downloaded from public Hunspell dictionary repositories during CI/publish builds:

- https://github.com/LibreOffice/dictionaries
- https://github.com/wooorm/dictionaries

Each dictionary remains under its upstream license. See the matching upstream directory for authorship and license details.
SOURCES
