#!/bin/bash
# STAR Watch — Salesforce live demo
# Creates a new Opportunity and moves it through stages
# to demonstrate real-time detection without touching the Salesforce UI.

SF_ORG="star-watch"

echo ""
echo "★ STAR Watch — Salesforce Demo"
echo "─────────────────────────────────"
echo ""

echo "→ Creating new Opportunity: 'Enterprise SOP Deal'..."
ID=$(sf data create record \
  --target-org $SF_ORG \
  --sobject Opportunity \
  --values "Name='Enterprise SOP Deal' StageName='Prospecting' CloseDate=2026-12-31 Amount=250000" \
  --json | python3 -c "import sys,json; print(json.load(sys.stdin)['result']['id'])")

echo "  Created: $ID"
echo ""
sleep 3

echo "→ Moving to Qualification..."
sf data update record --target-org $SF_ORG --sobject Opportunity --record-id $ID \
  --values "StageName='Qualification'" --json > /dev/null
sleep 5

echo "→ Moving to Proposal..."
sf data update record --target-org $SF_ORG --sobject Opportunity --record-id $ID \
  --values "StageName='Proposal/Price Quote'" --json > /dev/null
sleep 5

echo "→ Moving to Closed Won!"
sf data update record --target-org $SF_ORG --sobject Opportunity --record-id $ID \
  --values "StageName='Closed Won'" --json > /dev/null

echo ""
echo "Done. Check the STAR Watch logs — each stage change should appear within 8s."
echo ""
