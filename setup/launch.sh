#!/bin/sh
# One entry point; no activation, device selection or global package installation.
set -eu
here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec "${PYTHON:-python3}" -I "$here/launcher.py" "$@"
