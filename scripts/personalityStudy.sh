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

# Dynamically read actor names from Archetypes.cs so this list stays in sync.
# Matches C# dictionary entries of the form: ["Name"] = new()
# Requires GNU grep (standard on Linux; macOS ships GNU grep via Homebrew or
# the system grep on macOS 13+ also supports -P).
ARCHETYPES_FILE="src/JohnnyLike.SimRunner/Archetypes.cs"
mapfile -t ACTORS < <(grep -oP '(?<=\[")[^"]+(?="\]\s*=\s*new\s*\(\))' "${ARCHETYPES_FILE}")

if [[ ${#ACTORS[@]} -eq 0 ]]; then
    echo "Error: could not read actor names from ${ARCHETYPES_FILE}" >&2
    exit 1
fi

TIMESTAMP=$(date -u +"%Y%m%d-%H%M%S")
STUDY_SUBDIR="personality-study-seed${SEED}-${TIMESTAMP}"

echo "=== Personality Study ==="
echo "Seed:     ${SEED}"
echo "Duration: ${DURATION}s"
echo "Actors:   ${ACTORS[*]}"
echo "Subdir:   artifacts/${STUDY_SUBDIR}"
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
        --save-artifacts "${STUDY_SUBDIR}"
    echo ""
done

echo "=== All simulations complete. Archiving artifacts... ==="

STUDY_DIR="artifacts/${STUDY_SUBDIR}"
ZIP_NAME="artifacts/personality-study-seed${SEED}-${TIMESTAMP}.zip"

# Collect all files produced in the study subfolder.
# Using -print0 / mapfile -d '' to correctly handle filenames with spaces or
# special characters.
mapfile -d '' -t ARTIFACT_FILES < <(find "${STUDY_DIR}" -type f -print0 | sort -z)

if [[ ${#ARTIFACT_FILES[@]} -eq 0 ]]; then
    echo "No artifact files found to archive." >&2
    exit 1
fi

zip "${ZIP_NAME}" "${ARTIFACT_FILES[@]}"

echo ""
echo "Archived ${#ARTIFACT_FILES[@]} file(s) to: ${ZIP_NAME}"
