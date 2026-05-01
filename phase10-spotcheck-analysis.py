#!/usr/bin/env python
"""
Phase 10B: Comprehensive Spot-Check & Re-Labeling Analysis

Analyzes false positive samples to understand label/detection mismatches.

Usage:
  python phase10-spotcheck-analysis.py <rule_id> [sample_count]
"""

import sqlite3
import json
from pathlib import Path
import sys

class SpotCheckAnalyzer:
    def __init__(self, db_path='data/gauntletci-corpus.db'):
        self.conn = sqlite3.connect(db_path)
        self.conn.row_factory = sqlite3.Row
        self.cur = self.conn.cursor()
    
    def get_false_positives(self, rule_id, limit=10):
        """Get false positive samples where corpus label says NO but rule says YES."""
        self.cur.execute("""
            SELECT 
              af.fixture_id,
              f.repo,
              f.pr_number,
              f.language,
              f.tags_json,
              f.has_tests_changed,
              f.has_review_comments,
              COUNT(*) as detection_count
            FROM actual_findings af
            LEFT JOIN expected_findings ef 
              ON ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
            JOIN fixtures f ON f.fixture_id = af.fixture_id
            WHERE af.rule_id = ? AND ef.id IS NULL
            GROUP BY af.fixture_id
            ORDER BY RANDOM()
            LIMIT ?
        """, (rule_id, limit))
        return self.cur.fetchall()
    
    def get_true_positives(self, rule_id, limit=5):
        """Get true positive samples where corpus label says YES and rule says YES."""
        self.cur.execute("""
            SELECT 
              af.fixture_id,
              f.repo,
              f.pr_number,
              f.language,
              f.tags_json,
              f.has_tests_changed,
              f.has_review_comments
            FROM actual_findings af
            JOIN expected_findings ef 
              ON ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
            JOIN fixtures f ON f.fixture_id = af.fixture_id
            WHERE af.rule_id = ? AND ef.id IS NOT NULL
            GROUP BY af.fixture_id
            ORDER BY RANDOM()
            LIMIT ?
        """, (rule_id, limit))
        return self.cur.fetchall()
    
    def analyze_rule_fp_pattern(self, rule_id, limit=10):
        """Analyze FP patterns for a rule."""
        print('\n' + '='*90)
        print(f'PHASE 10B SPOT-CHECK: {rule_id}')
        print('='*90)
        
        # Get corpus statistics
        self.cur.execute("""
            SELECT 
              COUNT(DISTINCT af.fixture_id) as actual_count,
              COUNT(DISTINCT ef.fixture_id) as expected_count
            FROM (SELECT * FROM actual_findings WHERE rule_id = ?) af
            LEFT JOIN (SELECT * FROM expected_findings WHERE rule_id = ?) ef
              ON af.fixture_id = ef.fixture_id
        """, (rule_id, rule_id))
        
        stats = self.cur.fetchone()
        actual = stats['actual_count']
        expected = stats['expected_count']
        
        # Get FP/TP breakdown
        fps = self.get_false_positives(rule_id, limit)
        tps = self.get_true_positives(rule_id, 5)
        
        print(f'\nCORPUS STATISTICS:')
        print(f'  Expected (in corpus):     {expected}')
        print(f'  Actual (rule detected):   {actual}')
        print(f'  True Positives:           {len(tps)}')
        print(f'  False Positives:          {len(fps)}')
        if actual > 0:
            print(f'  Precision:                {len(tps) / actual * 100:.1f}%')
        print()
        
        # Show comparison: TP vs FP patterns
        print('='*90)
        print('TRUE POSITIVE SAMPLES (Rule correct, corpus label correct):')
        print('='*90)
        for i, tp in enumerate(tps, 1):
            tags = json.loads(tp['tags_json'] or '[]') if tp['tags_json'] else []
            print(f'\nTP{i}: {tp["repo"]} PR#{tp["pr_number"]} ({tp["language"]})')
            print(f'     Tags: {", ".join(tags) if tags else "[none]"}')
            print(f'     Tests: {"CHANGED" if tp["has_tests_changed"] else "unchanged"}, Review: {"YES" if tp["has_review_comments"] else "NO"}')
        
        print('\n' + '='*90)
        print(f'FALSE POSITIVE SAMPLES (Rule triggered, corpus label says NO):')
        print('='*90)
        for i, fp in enumerate(fps, 1):
            tags = json.loads(fp['tags_json'] or '[]') if fp['tags_json'] else []
            print(f'\nFP{i}: {fp["repo"]} PR#{fp["pr_number"]} ({fp["language"]})')
            print(f'     Tags: {", ".join(tags) if tags else "[none]"}')
            print(f'     Tests: {"CHANGED" if fp["has_tests_changed"] else "unchanged"}, Review: {"YES" if fp["has_review_comments"] else "NO"}')
        
        # Pattern analysis
        print('\n' + '='*90)
        print('PATTERN ANALYSIS:')
        print('='*90)
        
        # Group FPs by metadata
        by_lang = {}
        by_tag = {}
        for fp in fps:
            lang = fp['language']
            by_lang[lang] = by_lang.get(lang, 0) + 1
            tags = json.loads(fp['tags_json'] or '[]') if fp['tags_json'] else []
            for tag in tags:
                by_tag[tag] = by_tag.get(tag, 0) + 1
        
        print(f'\nFalse Positives by Language:')
        for lang, count in sorted(by_lang.items(), key=lambda x: -x[1]):
            print(f'  {lang}: {count}/{len(fps)}')
        
        if by_tag:
            print(f'\nMost Common Tags in FP Samples:')
            for tag, count in sorted(by_tag.items(), key=lambda x: -x[1])[:8]:
                print(f'  {tag}: {count}/{len(fps)}')
        
        print('\n' + '='*90)
        print('ASSESSMENT QUESTIONS:')
        print('='*90)
        print('''
For each FP sample, decide:
  [L] Label Wrong     - Old label missed a real problem; rule is correct
  [S] Rule Stricter   - Rule detects more; old label was narrower
  [B] Behavior Changed- Rule logic fundamentally different; incomparable
  [N] New/Post-Label  - Rule added/changed after corpus was labeled
  [D] Design Choice   - Rule designed to be stricter for safety

Decision impacts corpus re-labeling:
  - If [L] or [S]: Update expected_findings to mark as SHOULD_BE_FLAGGED
  - If [B] or [N]: Document as "corpus labels may be outdated"
  - If [D]: Document as "rule is intentionally stricter than corpus"

Next step: Manually review these samples and categorize each.
Run: python phase10-categorize-samples.py <rule_id> <categories_json>
''')
        
        return {'fps': fps, 'tps': tps}
    
    def close(self):
        self.conn.close()


def main():
    if len(sys.argv) < 2:
        print('Usage: python phase10-spotcheck-analysis.py <rule_id> [sample_count]')
        print('Examples:')
        print('  python phase10-spotcheck-analysis.py GCI0003 10')
        print('  python phase10-spotcheck-analysis.py GCI0004 15')
        sys.exit(1)
    
    rule_id = sys.argv[1]
    sample_count = int(sys.argv[2]) if len(sys.argv) > 2 else 10
    
    analyzer = SpotCheckAnalyzer()
    result = analyzer.analyze_rule_fp_pattern(rule_id, sample_count)
    analyzer.close()
    
    # Save for later analysis
    with open(f'phase10-spotcheck-{rule_id}.json', 'w') as f:
        json.dump({
            'rule_id': rule_id,
            'sample_count': sample_count,
            'fp_samples': [dict(fp) for fp in result['fps']],
            'tp_samples': [dict(tp) for tp in result['tps']],
        }, f, indent=2, default=str)
    
    print(f'\nResults saved to: phase10-spotcheck-{rule_id}.json')


if __name__ == '__main__':
    main()
