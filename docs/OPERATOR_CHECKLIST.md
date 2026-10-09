# Operator checklist (Ashley)

Draft. Ashley owns this file; edit it after the pilot.
About 50-55 minutes per participant.

## Google Form setup (once)
Make one form. Question titles must contain these words so `analysis/ssq.py` can read the export:

| Question title | Type |
|---|---|
| Participant ID | short answer (P01, P02, ...) |
| Timepoint | multiple choice: baseline, after_1, after_2, after_3 |
| General discomfort, Fatigue, Headache, Eyestrain, Difficulty focusing, Increased salivation, Sweating, Nausea, Difficulty concentrating, Fullness of head, Blurred vision, Dizzy (eyes open), Dizzy (eyes closed), Vertigo, Stomach awareness, Burping | 16 multiple-choice grid rows or questions: None / Slight / Moderate / Severe |
| Rank: Joystick, Rank: Teleport, Rank: Trail | 1 / 2 / 3, only on after_3 (use a section that shows only for after_3) |
| Most comfortable method | joystick / teleport / trail, only on after_3 |
| Comment | paragraph, optional |

Baseline section can also ask age range, prior VR use, and "do you get motion sick easily?" (pending team decision).
Link the form to a Google Sheet. After each session day: File > Download > CSV and send it (or upload to `Team8/data/forms/`).

## Before the participant arrives
- [ ] Headset charged over 70%, lenses clean, controllers charged
- [ ] Guardian set (stationary/seated), chair with no swivel in the middle of the clear area
- [ ] App version on the lobby panel matches the latest GitHub Release
- [ ] Form open on laptop or phone
- [ ] Consent form ready (if required)
- [ ] Next participant ID picked from the session log (P01, P02, ... never reuse)

## Session
1. **Baseline SSQ** in the form (headset off), Timepoint = baseline.
2. You put on the headset, open the app. On the lobby panel:
   - set Participant with the arrows (e.g. P05)
   - check the order line, e.g. `1. Teleport/A  2. Teleport + Trail/B  3. Joystick/C`
   - Start trial = 1 (only change this when resuming after a crash)
3. Hand over the headset. Read the script:
   > "You'll do three short rounds, each with a different way of moving. In each round there are three rooms. Each room has a hidden lever. Find it and pull it to open the gate to the next room. The third lever opens the exit. Get out as fast as you can. You have up to five minutes. First you'll get a short practice. Tell me any time you want to stop."
4. Press **Practice** (or have them press it). Explain this round's controls:
   - Joystick: push the left stick to walk, right stick to turn.
   - Teleport / Teleport + Trail: push the left stick forward, aim at the floor, let go to jump. Right stick to turn.
   - (Trail only) "The dots and line on the floor show where you've already been."
5. When they've tried it and pulled the practice lever, they press **Start**.
6. Trial runs (max 5 min). **No hints. Don't take the headset off mid-trial** (it pauses the app).
7. App returns to the lobby on its own. Headset off.
8. **SSQ** in the form with Timepoint = after_1 (then after_2, after_3).
9. Break until 5 minutes have passed since the trial ended.
10. Repeat steps 3-9 for rounds 2 and 3. The app already knows the next method.
11. After the after_3 SSQ: ranking + most comfortable + comment (same form).

**If they feel sick:** stop right away (operator panel **Abort**), headset off, water, sit. Note it in the session log. Don't push them to continue.
**If the app crashes:** reopen, set the same Participant, set Start trial to the round that crashed, continue. The app makes a new data folder; the analysis handles it.

## After the session (same day)
1. Plug the headset into the laptop.
2. Copy the data off, either:
   - MQDH: Device Manager > File Manager (turn on "Show app data") > `Android/data/edu.colostate.cs464.team8/files/StudyData/` > drag the new `P05_...` folder out, or
   - `adb pull /sdcard/Android/data/edu.colostate.cs464.team8/files/StudyData ./StudyData`
3. Upload the folder to the shared Drive `Team8/data/raw/`.
4. Add a line to the session log sheet: ID, date, any problems (sickness, crash, interruptions).
5. Don't delete anything from the headset until Mahanyas confirms it's committed.
