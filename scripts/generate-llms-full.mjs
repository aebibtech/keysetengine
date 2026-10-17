import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');
const docsDir = path.resolve(rootDir, 'docs');
const outputDir = path.resolve(docsDir, 'public');
const outputFile = path.resolve(outputDir, 'llms-full.txt');

// Logical reading order for AI comprehension
const docFiles = [
  'index.md',
  'guide/getting-started.md',
  'guide/why-keyset.md',
  'guide/faq.md',
  'guide/architecture.md',
  'guide/bidirectional.md',
  'guide/search.md',
  'guide/indexing.md',
  'security/data-protection.md',
  'databases/postgresql.md',
  'databases/sqlserver.md',
  'databases/sqlite.md',
  'api/core.md',
  'api/data-protection.md',
  'api/postgresql.md',
  'api/sqlserver.md'
];

function stripFrontmatter(content) {
  if (content.startsWith('---')) {
    const end = content.indexOf('---', 3);
    if (end !== -1) {
      return content.slice(end + 3).trim();
    }
  }
  return content.trim();
}

let fullText = `# KeysetEngine — Complete Documentation & Technical Reference
Repository: https://github.com/aebibtech/keysetengine
Documentation: https://keysetengine.aebibtech.com
License: MIT
Target Frameworks: .NET 8.0, .NET 9.0, .NET 10.0

This file provides the complete, concatenated documentation for KeysetEngine formatted as unformatted Markdown for LLMs and AI coding assistants.

================================================================================
TABLE OF CONTENTS
${docFiles.map((f, i) => `${i + 1}. ${f}`).join('\n')}
================================================================================

`;

for (const relPath of docFiles) {
  const absPath = path.resolve(docsDir, relPath);
  if (!fs.existsSync(absPath)) {
    console.warn(`Warning: ${relPath} not found, skipping.`);
    continue;
  }

  const raw = fs.readFileSync(absPath, 'utf8');
  const clean = stripFrontmatter(raw);

  fullText += `\n\n${'='.repeat(80)}\n`;
  fullText += `FILE: ${relPath}\n`;
  fullText += `${'='.repeat(80)}\n\n`;
  fullText += clean + '\n';
}

if (!fs.existsSync(outputDir)) {
  fs.mkdirSync(outputDir, { recursive: true });
}

fs.writeFileSync(outputFile, fullText, 'utf8');
const wordCount = fullText.split(/\s+/).length;
console.log(`Successfully generated ${outputFile} (${wordCount.toLocaleString()} words).`);
