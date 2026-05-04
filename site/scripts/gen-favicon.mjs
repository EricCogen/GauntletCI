#!/usr/bin/env node
import sharp from 'sharp';
import { promises as fs } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const publicDir = join(__dirname, '..', 'public');

async function generateFavicon() {
  try {
    // Read the 512x512 PNG
    const inputPath = join(publicDir, 'icon-512x512.png');
    const outputPath = join(publicDir, 'favicon.ico');

    // Convert PNG to ICO (favicon standard sizes: 16x16, 32x32, 48x48)
    // We'll create a multi-resolution ICO with 32x32 as the primary size
    console.log('Generating favicon.ico from icon-512x512.png...');
    
    const image = sharp(inputPath);
    
    // Create 32x32 version for favicon
    const favicon = await image
      .resize(32, 32, {
        fit: 'contain',
        background: { r: 9, g: 24, b: 39, alpha: 1 } // Match the dark background
      })
      .png()
      .toBuffer();

    // Save as ICO (Note: ico format needs a proper encoder, but browsers accept PNG as favicon too)
    // For simplicity and broad compatibility, we'll use PNG but serve it as favicon.ico
    await fs.writeFile(outputPath, favicon);
    
    console.log(`✓ Created favicon.ico (32x32)`);
    console.log(`  Location: ${outputPath}`);
    console.log(`  Size: ${(favicon.length / 1024).toFixed(2)} KB`);

  } catch (error) {
    console.error('Error generating favicon:', error);
    process.exit(1);
  }
}

generateFavicon();
