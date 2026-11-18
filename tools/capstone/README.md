# Capstone helpers (episode 5.4)

Two small scripts used in the capstone walkthrough. Neither is part of the solution.

- `glossary-tour.sh <group>`: for each glossary term, prints the first real match in this repository (`file:line`),
  or says `not in this repository`. Groups: `lang`, `web`, `arch`, `quality`, `devops`. Read-only.
- `json-log-view.sh`: reads the JSON console log on stdin and prints the level, the correlation id and the message.
  Start the app with `LogFormat=json`, then `docker logs <container> 2>&1 | bash tools/capstone/json-log-view.sh`.
