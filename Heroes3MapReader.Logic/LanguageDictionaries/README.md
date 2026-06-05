# Public language dictionaries

This directory is intentionally kept without generated dictionary data in git. Release builds download real public Hunspell dictionaries for the VCMI translation languages by running:

```bash
scripts/download-language-dictionaries.sh
```

The script tries LibreOffice dictionaries first and falls back to `wooorm/dictionaries` where available. Downloaded `.dic` files are used as the Latin-script language fallback. NTextCat and script detection remain available for languages where no public Hunspell dictionary exists.
