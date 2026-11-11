#!/bin/sh
set -e

TARGET_FILE="/usr/share/nginx/html/config.js"
API_URL="${API_URL:-http://localhost:8080}"

cat <<EOF > "$TARGET_FILE"
window.__API_URL__ = "${API_URL}";
EOF

echo "Generated runtime config with API_URL: ${API_URL}"
