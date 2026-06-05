# Public language dictionaries

Place public Hunspell/OpenOffice/LibreOffice-style `.dic` dictionaries or plain one-word-per-line `.txt` word lists in this directory to improve Latin-script language fallback detection.

Supported file names are based on ISO language codes, for example:

- `cs_CZ.dic`, `cs.dic`, `ces.dic`, or `cze.dic` for Czech
- `en_US.dic`, `en_GB.dic`, `en.dic`, or `eng.dic` for English
- `pl_PL.dic`, `pl.dic`, or `pol.dic` for Polish
- `de_DE.dic`, `de.dic`, `deu.dic`, or `ger.dic` for German
- `fr_FR.dic`, `fr.dic`, `fra.dic`, or `fre.dic` for French
- `hu_HU.dic`, `hu.dic`, or `hun.dic` for Hungarian
- `sv_SE.dic`, `sv.dic`, or `swe.dic` for Swedish
- `es_ES.dic`, `es.dic`, or `spa.dic` for Spanish
- `it_IT.dic`, `it.dic`, or `ita.dic` for Italian

The application does not maintain a custom in-repository word list. NTextCat remains the primary detector, and these public dictionaries are used only as a fallback when they are present in the published output.
