#!/usr/bin/env python
"""
Phase 10A: Corpus Re-Labeling Tool

This tool systematically re-labels the corpus database against current rule implementations
to build a reliable ground truth baseline for Phase 10B/C analysis.

Usage:
  python phase10-relabel-tool.py [--mode analyze|validate|spot-check]

Modes:
  - analyze:     Analyze current label/detection mismatch (fast, no changes)
  - validate:    Compare corpus labels with actual detections (generates diff report)
  - spot-check:  Manually review mismatched samples for specific rules
"""

import sqlite3
import json
from collections import defaultdict
from pathlib import Path
from datetime import datetime

class CorpusReLabelTool:
    def __init__(self, db_path='data/gauntletci-corpus.db'):
        self.conn = sqlite3.connect(db_path)
        self.conn.row_factory = sqlite3.Row
        self.cur = self.conn.cursor()
    
    def analyze_mismatch(self):
        """Analyze label/detection mismatch without making changes."""
        print('\n' + '='*80)
        print('PHASE 10A: CORPUS RE-LABELING ANALYSIS')
        print('='*80)
        print(f'Generated: {datetime.now().isoformat()}\n')
        
        # Get all rules
        self.cur.execute("""
            SELECT DISTINCT rule_id FROM actual_findings 
            WHERE rule_id LIKE 'GCI%' 
            ORDER BY rule_id
        """)
        rules = [row[0] for row in self.cur.fetchall()]
        
        print(f'Total rules in corpus: {len(rules)}\n')
        
        # Analyze each rule
        results = []
        for rule_id in rules:
            self.cur.execute("""
                SELECT 
                  ef.rule_id,
                  COUNT(DISTINCT ef.fixture_id) as expected_count,
                  COUNT(DISTINCT af.fixture_id) as actual_count,
                  COUNT(DISTINCT CASE 
                    WHEN af.fixture_id IS NOT NULL THEN ef.fixture_id 
                  END) as true_positives,
                  COUNT(DISTINCT CASE 
                    WHEN af.fixture_id IS NULL THEN ef.fixture_id 
                  END) as false_negatives,
                  COUNT(DISTINCT CASE 
                    WHEN ef.fixture_id IS NULL THEN af.fixture_id 
                  END) as false_positives
                FROM (SELECT rule_id, fixture_id FROM expected_findings WHERE rule_id = ?) ef
                FULL OUTER JOIN (SELECT rule_id, fixture_id FROM actual_findings WHERE rule_id = ?) af
                  ON ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
            """, (rule_id, rule_id))
            
            row = self.cur.fetchone()
            if row:
                expected = row['expected_count'] or 0
                actual = row['actual_count'] or 0
                tp = row['true_positives'] or 0
                fn = row['false_negatives'] or 0
                fp = row['false_positives'] or 0
                
                precision = (tp / actual * 100) if actual > 0 else 0
                recall = (tp / expected * 100) if expected > 0 else 0
                
                results.append({
                    'rule_id': rule_id,
                    'expected': expected,
                    'actual': actual,
                    'tp': tp,
                    'fn': fn,
                    'fp': fp,
                    'precision': precision,
                    'recall': recall,
                })
        
        # Sort by FP rate (worst first)
        results_by_fp = sorted(results, key=lambda x: x['fp'], reverse=True)
        
        print('TOP 20 RULES BY FALSE POSITIVE COUNT (Worst Mismatches)')
        print('-'*80)
        print(f"{'Rule':<12} {'Expected':<10} {'Actual':<10} {'TP':<6} {'FP':<6} {'FN':<6} {'Precision':<12} {'Recall':<12}")
        print('-'*80)
        for r in results_by_fp[:20]:
            print(f"{r['rule_id']:<12} {r['expected']:<10} {r['actual']:<10} {r['tp']:<6} {r['fp']:<6} {r['fn']:<6} {r['precision']:>10.1f}% {r['recall']:>10.1f}%")
        
        print('\n' + '='*80)
        print('RULES WITH 100% FALSE POSITIVE RATE (Complete Label/Implementation Mismatch)')
        print('='*80 + '\n')
        
        fp_100 = [r for r in results if r['fp'] > 0 and r['actual'] > 0 and r['precision'] == 0]
        print(f'Count: {len(fp_100)} rules\n')
        for r in fp_100:
            print(f"  {r['rule_id']:<12} Expected: {r['expected']:>3}  Actual: {r['actual']:>3}  FP Rate: 100.0%")
        
        print('\n' + '='*80)
        print('RULES WITH 100% FALSE NEGATIVE RATE (Expected But Never Detected)')
        print('='*80 + '\n')
        
        fn_100 = [r for r in results if r['expected'] > 0 and r['actual'] == 0]
        print(f'Count: {len(fn_100)} rules\n')
        for r in fn_100:
            print(f"  {r['rule_id']:<12} Expected: {r['expected']:>3}  Detected: {r['actual']:>3}  FN Rate: 100.0%")
        
        print('\n' + '='*80)
        print('RULES WITH PARTIAL MISMATCH (30-90% FP Rate)')
        print('='*80 + '\n')
        
        partial = [r for r in results if 30 <= r['precision'] < 90 and r['actual'] > 0]
        print(f'Count: {len(partial)} rules\n')
        for r in sorted(partial, key=lambda x: x['precision']):
            print(f"  {r['rule_id']:<12} Precision: {r['precision']:>6.1f}%  Recall: {r['recall']:>6.1f}%  (TP={r['tp']}, FP={r['fp']}, FN={r['fn']})")
        
        # Summary stats
        print('\n' + '='*80)
        print('SUMMARY STATISTICS')
        print('='*80 + '\n')
        
        total_expected = sum(r['expected'] for r in results)
        total_actual = sum(r['actual'] for r in results)
        total_tp = sum(r['tp'] for r in results)
        total_fp = sum(r['fp'] for r in results)
        total_fn = sum(r['fn'] for r in results)
        
        overall_precision = (total_tp / total_actual * 100) if total_actual > 0 else 0
        overall_recall = (total_tp / total_expected * 100) if total_expected > 0 else 0
        
        print(f'Total expected findings:      {total_expected:>6}')
        print(f'Total actual detections:     {total_actual:>6}')
        print(f'True positives (TP):         {total_tp:>6}')
        print(f'False positives (FP):        {total_fp:>6}')
        print(f'False negatives (FN):        {total_fn:>6}')
        print(f'\nOverall precision:           {overall_precision:>6.1f}%')
        print(f'Overall recall:              {overall_recall:>6.1f}%')
        
        rules_100_fp = len([r for r in results if r['actual'] > 0 and r['precision'] == 0])
        rules_0_tp = len([r for r in results if r['tp'] == 0])
        
        print(f'\nRules with 100% FP rate:     {rules_100_fp:>6}')
        print(f'Rules with 0% TP rate:       {rules_0_tp:>6}')
        
        return results
    
    def spot_check_fixtures(self, rule_id, limit=5):
        """Show sample fixtures where labels and detections mismatch."""
        print(f'\n' + '='*80)
        print(f'SPOT-CHECK: {rule_id} - False Positive Samples')
        print('='*80 + '\n')
        
        self.cur.execute("""
            SELECT DISTINCT af.fixture_id, f.fixture_id as fname, f.repo, f.pr_number
            FROM actual_findings af
            LEFT JOIN expected_findings ef 
              ON ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
            JOIN fixtures f ON f.fixture_id = af.fixture_id
            WHERE af.rule_id = ? AND ef.id IS NULL
            GROUP BY af.fixture_id
            LIMIT ?
        """, (rule_id, limit))
        
        fixtures = self.cur.fetchall()
        print(f'Found {len(fixtures)} false positive samples for {rule_id}\n')
        
        for i, fix in enumerate(fixtures, 1):
            fixture_id = fix['fixture_id']
            fname = fix['fname']
            repo = fix['repo']
            pr = fix['pr_number']
            
            print(f'Sample {i}/{len(fixtures)}: {repo} PR#{pr}')
            print('-' * 80)
            print(f'Fixture: {fname}')
            print(f'Status: {rule_id} flagged (corpus label says it should NOT be)\n')
    
    def close(self):
        self.conn.close()


def main():
    import sys
    
    mode = 'analyze' if len(sys.argv) < 2 else sys.argv[1].replace('--mode=', '').replace('--', '')
    
    tool = CorpusReLabelTool()
    
    if mode == 'analyze':
        results = tool.analyze_mismatch()
        
        # Save to CSV for later analysis
        import csv
        with open('corpus-relabel-analysis.csv', 'w', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=['rule_id', 'expected', 'actual', 'tp', 'fn', 'fp', 'precision', 'recall'])
            writer.writeheader()
            writer.writerows(results)
        print(f'\nAnalysis saved to: corpus-relabel-analysis.csv\n')
    
    elif mode == 'spot-check':
        if len(sys.argv) < 3:
            print('Usage: python phase10-relabel-tool.py --mode spot-check <rule_id> [limit]')
            sys.exit(1)
        
        rule_id = sys.argv[2]
        limit = int(sys.argv[3]) if len(sys.argv) > 3 else 5
        tool.spot_check_fixtures(rule_id, limit)
    
    else:
        print('Usage: python phase10-relabel-tool.py [--mode analyze|spot-check]')
        sys.exit(1)
    
    tool.close()


if __name__ == '__main__':
    main()
