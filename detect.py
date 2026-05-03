import json
from graphify.detect import detect
from pathlib import Path

result = detect(Path('.'))
with open('graphify-out/graphify_detect.json', 'w') as f:
    json.dump(result, f, indent=2)
print(f"Detected: {result['total_files']} files, {result['total_words']:,} words")
for category, count in [(k, len(v)) for k, v in result.get('files', {}).items() if v]:
    print(f"  {category}: {count}")
