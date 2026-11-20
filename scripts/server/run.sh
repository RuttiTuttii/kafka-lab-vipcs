#!/usr/bin/env bash
# usage: ./scripts/server/run.sh user@host diag|deploy [--yes] [suffix]
set -eu
target="${1:?usage: run.sh user@host diag|deploy [--yes] [suffix]}"
action="${2:?action: diag|deploy}"
yes="${3:-}"
suffix="${4:-2}"
here="$(cd "$(dirname "$0")" && pwd)"
host_ip="${target#*@}"

case "$action" in
  diag)   ssh "$target" 'bash -s' < "$here/remote-diag.sh" ;;
  deploy) ssh "$target" "HOST_IP=$host_ip SUFFIX=$suffix YES=$yes bash -s" < "$here/remote-deploy.sh" ;;
  *) echo "unknown action: $action"; exit 1 ;;
esac
