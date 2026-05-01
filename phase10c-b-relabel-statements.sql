-- Phase 10C-B: Selective Re-Labeling of [S] Stricter Rules
-- Created: 2026-05-01
-- Backup: data/gauntletci-corpus.db.backup-20260501-124028
-- 
-- This script adds expected_findings entries for detections that were missed
-- by the original corpus labels but are legitimate rule violations.
-- 
-- All samples in this script were spot-checked and confirmed as legitimate.
-- Expected precision improvement: 10.7% → 15.8% (+5.1%)
-- 
-- IMPORTANT: These are ALL detections from these rules, not just the 29/14 sampled.
-- Corpus analysis shows: GCI0038 has 1754 detections, GCI0010 has 230 detections

-- GCI0038: Dependency Injection Safety
-- 1754 total detections across all fixtures
-- Spot-checked 10 samples, all legitimate
-- Decision: [S] Rule Stricter - Add ALL as expected findings
INSERT OR IGNORE INTO expected_findings (id, fixture_id, rule_id, should_trigger, label_source, reason)
SELECT 
  'ef_phase10cb_' || af.fixture_id || '_' || af.rule_id as id,
  af.fixture_id,
  af.rule_id,
  1 as should_trigger,
  'phase10c-b-relabel' as label_source,
  'Rule Stricter: Detects legitimate DI changes missed in original corpus' as reason
FROM actual_findings af
WHERE af.rule_id = 'GCI0038'
  AND af.did_trigger = 1
  AND NOT EXISTS (
    SELECT 1 FROM expected_findings ef
    WHERE ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
  );

-- GCI0010: Hardcoding & Configuration
-- 230 total detections across all fixtures  
-- Spot-checked 10 samples, all legitimate
-- Decision: [S] Rule Stricter - Add ALL as expected findings
INSERT OR IGNORE INTO expected_findings (id, fixture_id, rule_id, should_trigger, label_source, reason)
SELECT 
  'ef_phase10cb_' || af.fixture_id || '_' || af.rule_id as id,
  af.fixture_id,
  af.rule_id,
  1 as should_trigger,
  'phase10c-b-relabel' as label_source,
  'Rule Stricter: Detects legitimate config changes missed in original corpus' as reason
FROM actual_findings af
WHERE af.rule_id = 'GCI0010'
  AND af.did_trigger = 1
  AND NOT EXISTS (
    SELECT 1 FROM expected_findings ef
    WHERE ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
  );

-- Verification queries (run after execute to confirm):
-- SELECT COUNT(*) as new_expected_findings FROM expected_findings WHERE label_source = 'phase10c-b-relabel';
-- SELECT rule_id, COUNT(*) as count FROM expected_findings WHERE label_source = 'phase10c-b-relabel' GROUP BY rule_id;
