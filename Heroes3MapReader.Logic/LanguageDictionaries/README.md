# Public language dictionaries

This directory is intentionally kept without generated dictionary data in git. Release builds download real public Hunspell dictionaries from the LibreOffice dictionaries repository by running:

```bash
scripts/download-language-dictionaries.sh
```

The downloaded `.dic` files are used as the Latin-script language fallback. NTextCat remains available as the statistical detector, but the application no longer maintains a custom in-repository word list.
