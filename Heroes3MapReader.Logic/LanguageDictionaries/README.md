# Public language dictionaries

This directory is intentionally kept without generated dictionary data in git. Release builds download all available public Hunspell `.dic` dictionaries from the LibreOffice dictionaries repository by running:

```bash
scripts/download-language-dictionaries.sh
```

The script downloads the LibreOffice repository archive and copies every non-hyphenation `.dic` dictionary into this directory. The application then infers the language from each dictionary file name at runtime, so Czech, German and the other LibreOffice dictionaries do not need to be maintained manually in this repository.
