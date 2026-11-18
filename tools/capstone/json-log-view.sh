#!/bin/bash
# Reads the JSON console log (one JSON object per line) on stdin and prints three fields per line:
# the level, the correlation id carried by the request scope, and the message.
#   docker logs <container> 2>&1 | bash tools/capstone/json-log-view.sh
sed -nE 's/.*"LogLevel":"([A-Za-z]+)".*"Message":"([^"]*)","State".*"CorrelationId":"([^"]*)".*/\1  \3  \2/p'
