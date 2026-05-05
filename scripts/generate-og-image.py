#!/usr/bin/env python3
"""Generate a creative OG image for the determinism article."""

from PIL import Image, ImageDraw, ImageFont
import math
import os

# Image dimensions (Open Graph standard: 1200x630)
width, height = 1200, 630

# Modern color palette (RGB tuples for drawing)
bg_dark = (10, 14, 39)  # Deep navy
bg_accent = (15, 21, 52)  # Slightly lighter
cyan = (0, 217, 255)  # Primary cyan
purple = (124, 58, 237)  # Purple accent
pink = (236, 72, 153)  # Pink accent
text_white = (255, 255, 255)
text_gray = (139, 148, 180)

# Create image
img = Image.new("RGB", (width, height), bg_dark)
draw = ImageDraw.Draw(img, "RGBA")

# Load fonts
try:
    title_font = ImageFont.truetype("C:\\Windows\\Fonts\\segoeui.ttf", 72)
    subtitle_font = ImageFont.truetype("C:\\Windows\\Fonts\\segoeui.ttf", 28)
    label_font = ImageFont.truetype("C:\\Windows\\Fonts\\segoeuib.ttf", 20)
    small_font = ImageFont.truetype("C:\\Windows\\Fonts\\segoeui.ttf", 18)
except:
    title_font = ImageFont.load_default()
    subtitle_font = ImageFont.load_default()
    label_font = ImageFont.load_default()
    small_font = ImageFont.load_default()

# Draw animated-style gradient background (left to right)
for x in range(width):
    # Gradient from dark to darker with hint of purple
    ratio = x / width
    r = int(10 + ratio * 5)
    g = int(14 + ratio * 2)
    b = int(39 + ratio * 8)
    draw.line([(x, 0), (x, height)], fill=(r, g, b))

# Draw organic curved shapes using Bezier-like lines for visual interest
def draw_wave(y_start, amplitude, frequency, color, alpha):
    """Draw a wavy line across the image."""
    points = []
    for x in range(0, width + 50, 5):
        y = y_start + amplitude * math.sin(x / frequency) 
        points.append((x, int(y)))
    
    for i in range(len(points) - 1):
        r, g, b = color
        draw.line([points[i], points[i+1]], fill=(r, g, b, alpha), width=3)

# Draw flowing background waves (subtle)
draw_wave(200, 40, 150, cyan, 15)
draw_wave(150, 50, 200, purple, 12)
draw_wave(height - 100, 60, 180, pink, 10)

# Draw abstract dots/nodes pattern (representing network/determinism)
dot_positions = [
    (150, 100), (300, 180), (500, 110), (700, 160), (900, 90), (1050, 140),
    (200, 450), (450, 480), (650, 420), (850, 500), (1000, 450),
]
for x, y in dot_positions:
    r, g, b = cyan
    draw.ellipse([(x-4, y-4), (x+4, y+4)], fill=(r, g, b, 80))
    # Draw connecting lines to some neighbors
    if dot_positions.index((x, y)) % 3 == 0:
        next_idx = (dot_positions.index((x, y)) + 1) % len(dot_positions)
        nx, ny = dot_positions[next_idx]
        draw.line([(x, y), (nx, ny)], fill=(r, g, b, 30), width=1)

# Main content area
left_pad = 60
top_pad = 100

# Draw decorative accent line on left
draw.rectangle([(left_pad - 35, top_pad), (left_pad - 30, top_pad + 300)], 
               fill=cyan)

# Main title - split across lines
title_1 = "Can AI Code"
title_2 = "Review Be"
title_3 = "Deterministic?"

y_pos = top_pad
draw.text((left_pad, y_pos), title_1, font=title_font, fill=text_white)
y_pos += 80
draw.text((left_pad, y_pos), title_2, font=title_font, fill=text_white)
y_pos += 80

# Highlight last line with gradient effect (draw in cyan)
draw.text((left_pad, y_pos), title_3, font=title_font, fill=cyan)

# Draw three-layer architecture concept (right side)
arch_x = 750
arch_y = 120
box_width = 180
box_height = 50
gap = 15

boxes = [
    ("Deterministic", "Core", cyan),
    ("AI-Assisted", "Explanation", purple),
    ("Human", "Intent", pink),
]

for i, (label1, label2, color) in enumerate(boxes):
    y = arch_y + i * (box_height + gap)
    
    # Draw box
    draw.rectangle([
        (arch_x, y),
        (arch_x + box_width, y + box_height)
    ], outline=color, width=2)
    
    # Draw connecting arrows
    if i < len(boxes) - 1:
        r, g, b = color
        draw.line([
            (arch_x + box_width // 2, y + box_height),
            (arch_x + box_width // 2, y + box_height + gap - 5)
        ], fill=(r, g, b, 150), width=2)
        # Arrow head
        draw.polygon([
            (arch_x + box_width // 2 - 4, y + box_height + gap - 5),
            (arch_x + box_width // 2 + 4, y + box_height + gap - 5),
            (arch_x + box_width // 2, y + box_height + gap),
        ], fill=(r, g, b, 150))
    
    # Draw text
    draw.text((arch_x + 10, y + 8), label1, font=small_font, fill=text_white)
    draw.text((arch_x + 10, y + 28), label2, font=small_font, fill=color)

# Bottom tagline
tagline = "Repeatable evidence > Helpful suggestions"
tagline_y = height - 80
draw.text((left_pad, tagline_y), tagline, font=subtitle_font, fill=cyan)

# Brand
brand = "gauntletci.com"
draw.text((width - left_pad - 250, height - 40), brand, font=small_font, fill=text_gray)

# Save
output_path = "C:\\Users\\ericc\\source\\repos\\GauntletCI\\public\\og\\determinism-ai-review.png"
os.makedirs(os.path.dirname(output_path), exist_ok=True)
img.save(output_path, quality=95)

print(f"✓ Creative OG image generated: {output_path}")
print(f"  Features: flowing waves, node network, 3-layer architecture diagram")
