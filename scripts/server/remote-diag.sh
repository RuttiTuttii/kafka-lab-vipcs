#!/usr/bin/env bash
# read-only server diagnostics: changes nothing, only prints
echo "== host"; hostname; uname -sr; hostname -I 2>/dev/null
echo "== listening on 9092/9093/22"; ss -tlnp 2>/dev/null | grep -E ':(9092|9093|22)\b' || echo "nothing listens on these ports"
echo "== kafka processes"; ps aux | grep -i '[k]afka' | cut -c1-200 || true
echo "== systemd"; systemctl list-units --type=service 2>/dev/null | grep -i kafka || echo "no kafka unit"
echo "== docker"; (docker ps --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' 2>&1 | head -20) || echo "no docker"
echo "== java"; (java -version 2>&1 | head -1) || echo "no java"
echo "== firewall"; (sudo -n ufw status 2>&1 | head -15) || echo "ufw: no access"
echo "== disk and memory"; df -h / | tail -1; free -m | sed -n 2p
