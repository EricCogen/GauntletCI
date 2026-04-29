import fs from "fs";
import path from "path";

export type Section = "Added" | "Changed" | "Fixed" | "Deprecated" | "Removed" | "Security";

export interface ChangeEntry {
  text: string;
  link?: string;
}

export interface ReleaseSection {
  label: Section;
  entries: ChangeEntry[];
}

export interface Release {
  version: string;
  date: string;
  tag: string;
  summary: string;
  sections: ReleaseSection[];
  compareUrl?: string;
}

const VALID_SECTIONS: Set<string> = new Set(["Added", "Changed", "Fixed", "Deprecated", "Removed", "Security"]);

export function parseChangelog(): Release[] {
  // Try multiple paths to find CHANGELOG.md
  const possiblePaths = [
    path.join(process.cwd(), "..", "..", "CHANGELOG.md"),
    path.join(process.cwd(), "../../CHANGELOG.md"),
    path.join(process.cwd(), "../CHANGELOG.md"),
    path.join(process.cwd(), "CHANGELOG.md"),
  ];

  let content: string | null = null;
  let foundPath = "";

  for (const filePath of possiblePaths) {
    try {
      if (fs.existsSync(filePath)) {
        content = fs.readFileSync(filePath, "utf-8");
        foundPath = filePath;
        break;
      }
    } catch {
      // Continue to next path
    }
  }

  if (!content) {
    console.warn("CHANGELOG.md not found at any expected path");
    return [];
  }

  const releases: Release[] = [];
  const lines = content.split("\n");

  let currentVersion = "";
  let currentDate = "";
  let currentSectionLabel: Section | null = null;
  let currentSectionEntries: ChangeEntry[] = [];
  let allSections: ReleaseSection[] = [];

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const trimmed = line.trim();

    // Match version header: ## [1.2.3] - YYYY-MM-DD or ## [Unreleased]
    const versionMatch = trimmed.match(/^##\s+\[([^\]]+)\](?:\s+-\s+(\d{4}-\d{2}-\d{2}))?$/);
    if (versionMatch) {
      // Save previous release if any
      if (currentVersion && allSections.length > 0) {
        releases.push({
          version: currentVersion,
          date: currentDate,
          tag: currentVersion === "Unreleased" ? "unreleased" : releases.length === 0 ? "latest" : "",
          summary: generateSummary(allSections),
          sections: allSections,
          compareUrl: generateCompareUrl(currentVersion),
        });
      }

      currentVersion = versionMatch[1];
      currentDate = versionMatch[2] || "";
      allSections = [];
      currentSectionLabel = null;
      currentSectionEntries = [];
      continue;
    }

    // Match section header: ### Added, ### Changed, etc.
    const sectionMatch = trimmed.match(/^###\s+(\w+)$/);
    if (sectionMatch && VALID_SECTIONS.has(sectionMatch[1])) {
      // Save previous section if any
      if (currentSectionLabel && currentSectionEntries.length > 0) {
        allSections.push({
          label: currentSectionLabel,
          entries: currentSectionEntries,
        });
      }

      currentSectionLabel = sectionMatch[1] as Section;
      currentSectionEntries = [];
      continue;
    }

    // Match bullet point: - **Title**: Description or - Description
    if (trimmed.startsWith("- ") && currentSectionLabel) {
      const content = trimmed.substring(2).trim();
      const entry: ChangeEntry = {
        text: formatEntryText(content),
      };
      currentSectionEntries.push(entry);
      continue;
    }
  }

  // Save final release
  if (currentVersion && allSections.length > 0) {
    releases.push({
      version: currentVersion,
      date: currentDate,
      tag: currentVersion === "Unreleased" ? "unreleased" : releases.length === 0 ? "latest" : "",
      summary: generateSummary(allSections),
      sections: allSections,
      compareUrl: generateCompareUrl(currentVersion),
    });
  }

  return releases.filter((r) => r.version !== "Unreleased");
}

function formatEntryText(content: string): string {
  // Remove markdown bold markers but keep the text: **Title** -> Title:
  return content.replace(/\*\*([^*]+)\*\*:\s*/, "$1: ");
}

function generateSummary(sections: ReleaseSection[]): string {
  // Create a brief summary from the first entry of the first section
  if (sections.length === 0) return "Release";

  const firstEntry = sections[0].entries[0];
  if (!firstEntry) return "Release";

  let text = firstEntry.text;
  if (text.length > 100) {
    text = text.substring(0, 97) + "...";
  }
  return text;
}

function generateCompareUrl(version: string): string {
  if (version === "Unreleased") return "";

  // Find the next version to create a comparison URL
  // For now, use the tag-based comparison
  return `https://github.com/EricCogen/GauntletCI/releases/tag/v${version}`;
}
