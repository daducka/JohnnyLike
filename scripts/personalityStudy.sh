#!/usr/bin/env bash
set -euo pipefail

# Personality Study script
# Runs the SimRunner for each island actor and archives all generated artifacts.
#
# Usage: ./scripts/personalityStudy.sh [--seed <seed>] [--duration <seconds>]
#
# Defaults:
#   --seed     42
#   --duration 864000  (10 in-game days)

SEED=42
DURATION=864000

while [[ $# -gt 0 ]]; do
    case "$1" in
        --seed)
            SEED="$2"
            shift 2
            ;;
        --duration)
            DURATION="$2"
            shift 2
            ;;
        *)
            echo "Unknown argument: $1" >&2
            echo "Usage: $0 [--seed <seed>] [--duration <seconds>]" >&2
            exit 1
            ;;
    esac
done

# Resolve the repo root (the directory that contains this script's parent)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

ACTORS=(Johnny Frank Sawyer Ashley Oscar Elizabeth)
TIMESTAMP=$(date -u +"%Y%m%d-%H%M%S")

echo "=== Personality Study ==="
echo "Seed:     ${SEED}"
echo "Duration: ${DURATION}s"
echo "Actors:   ${ACTORS[*]}"
echo ""

for ACTOR in "${ACTORS[@]}"; do
    echo "--- Running simulation for actor: ${ACTOR} ---"
    dotnet run --project src/JohnnyLike.SimRunner -- \
        --domain island \
        --seed "${SEED}" \
        --trace \
        --actor "${ACTOR}" \
        --duration "${DURATION}" \
        --decision-verbose \
        --save-artifacts
    echo ""
done

echo "=== All simulations complete. Archiving artifacts... ==="

ARTIFACTS_DIR="artifacts"
mkdir -p "${ARTIFACTS_DIR}"

ZIP_NAME="${ARTIFACTS_DIR}/personality-study-seed${SEED}-${TIMESTAMP}.zip"

# Collect all non-zip files in the artifacts directory (top-level and failures/).
# Using -print0 / mapfile -d '' to correctly handle filenames with spaces or
# special characters.  Existing *.zip files are excluded so that prior study
# archives are never nested inside a new one.
mapfile -d '' -t ARTIFACT_FILES < <(find "${ARTIFACTS_DIR}" -type f ! -name "*.zip" -print0 | sort -z)

if [[ ${#ARTIFACT_FILES[@]} -eq 0 ]]; then
    echo "No artifact files found to archive." >&2
    exit 1
fi

zip "${ZIP_NAME}" "${ARTIFACT_FILES[@]}"

echo ""
echo "Archived ${#ARTIFACT_FILES[@]} file(s) to: ${ZIP_NAME}"
