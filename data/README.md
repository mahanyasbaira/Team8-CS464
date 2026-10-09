# data

- `raw/<P05_20261118-143012>/` : one folder per session, exactly as pulled off the headset (`positions.csv`, `events.csv`, `trials.csv`).
- `forms/ssq.csv` : Google Form export (File > Download > CSV from the linked Sheet).

Anonymous P-codes only. No names, emails, or notes that identify anyone.
Run `python analysis/analyze.py --data data --out results` to analyze.
