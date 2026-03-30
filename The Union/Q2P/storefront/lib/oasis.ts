/**
 * OASIS integration for Q2P - Quest proof entitlements
 *
 * - Fetches avatar's quest completion proofs from ONODE GET /api/quest/entitlements
 * - Applies discounts at checkout based on proof tier / physicalReward
 *
 * @see Docs/QUEST_TO_PHYSICAL_Q2P_BUILD_PLAN.md
 * @see Q2P/docs/LINK_STORE_TO_QUEST_SYSTEM_PLAN.md
 */

const ONODE_URL = process.env.NEXT_PUBLIC_OASIS_ONODE_URL ?? 'http://localhost:5004';

export interface QuestProof {
  proofId: string;
  gameId: string;
  objectiveKey: string;
  tier: 'entry' | 'intermediate' | 'master';
  physicalReward?: {
    type: 'discount' | 'sku' | 'exclusive';
    value: number;
    skuIds?: string[];
  };
  redeemed: boolean;
}

/** Get OASIS avatar JWT for entitlements. Dev: set NEXT_PUBLIC_OASIS_AVATAR_TOKEN. Production: set via cookie or portal embed. */
export function getOasisToken(): string | undefined {
  if (typeof window === 'undefined') return undefined;
  const envToken = process.env.NEXT_PUBLIC_OASIS_AVATAR_TOKEN;
  if (envToken) return envToken;
  const match = document.cookie.match(/oasis_token=([^;]+)/);
  return match?.[1] ? decodeURIComponent(match[1]) : undefined;
}

function parsePhysicalReward(raw: unknown): QuestProof['physicalReward'] | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  const o = raw as Record<string, unknown>;
  const type = o.type ?? o.Type;
  const value = o.value ?? o.Value;
  if (type !== 'discount' && type !== 'sku' && type !== 'exclusive') return undefined;
  const num = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(num)) return undefined;
  let skuIds: string[] | undefined;
  const s = o.skuIds ?? o.SkuIds;
  if (Array.isArray(s)) skuIds = s.map(String);
  return { type, value: num, skuIds };
}

function toQuestProof(row: Record<string, unknown>): QuestProof {
  const physicalReward = parsePhysicalReward(row.physicalReward ?? row.PhysicalReward)
    ?? (row.physicalRewardJson ?? row.PhysicalRewardJson
      ? parsePhysicalReward(safeParseJson(String(row.physicalRewardJson ?? row.PhysicalRewardJson)))
      : undefined);
  return {
    proofId: String(row.proofId ?? row.ProofId ?? ''),
    gameId: String(row.gameId ?? row.GameId ?? ''),
    objectiveKey: String(row.objectiveKey ?? row.ObjectiveKey ?? ''),
    tier: (row.tier ?? row.Tier ?? 'entry') as QuestProof['tier'],
    redeemed: Boolean(row.redeemed ?? row.Redeemed),
    physicalReward,
  };
}

function safeParseJson(s: string): unknown {
  try {
    return JSON.parse(s);
  } catch {
    return undefined;
  }
}

export async function getEntitlements(token: string): Promise<QuestProof[]> {
  const res = await fetch(`${ONODE_URL}/api/quest/entitlements`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) return [];
  const data = await res.json();
  const rawList = data?.result ?? data ?? [];
  if (!Array.isArray(rawList)) return [];
  return rawList.map((r: Record<string, unknown>) => toQuestProof(r));
}

/** Best discount % for this proof and sku (variant id or sku string). Empty skuIds = applies to all. */
export function getDiscountForProof(proof: QuestProof, skuId: string): number {
  if (proof.redeemed || !proof.physicalReward) return 0;
  if (proof.physicalReward.type === 'discount') {
    const skuIds = proof.physicalReward.skuIds;
    if (!skuIds || skuIds.length === 0 || skuIds.includes(skuId)) {
      return proof.physicalReward.value;
    }
  }
  return 0;
}

/**
 * Compute max quest discount % for a single cart line (tries variant id and variant sku).
 * Returns 0 if no proof applies.
 */
export function getQuestDiscountPercentForLine(
  proofs: QuestProof[],
  variantId: string,
  variantSku?: string | null
): number {
  let max = 0;
  for (const proof of proofs) {
    const byId = getDiscountForProof(proof, variantId);
    if (byId > max) max = byId;
    if (variantSku) {
      const bySku = getDiscountForProof(proof, variantSku);
      if (bySku > max) max = bySku;
    }
  }
  return max;
}

export interface CartQuestDiscountResult {
  totalDiscountAmount: number;
  currencyCode: string;
  appliedProofCount: number;
}

/**
 * Compute total quest discount for a cart (sum of line-level discounts).
 * Pass cart.lines and proofs from getEntitlements.
 */
export function computeCartQuestDiscount(
  lines: Array<{
    merchandise: { id: string };
    cost: { totalAmount: { amount: string; currencyCode: string } };
    variant?: { sku?: string | null } | null;
  }>,
  proofs: QuestProof[],
  currencyCode: string
): CartQuestDiscountResult {
  let totalDiscountAmount = 0;
  let appliedProofCount = 0;
  const usedProofIds = new Set<string>();
  for (const line of lines) {
    const variantId = line.merchandise?.id ?? '';
    const sku = line.variant?.sku;
    const pct = getQuestDiscountPercentForLine(proofs, variantId, sku);
    if (pct > 0) {
      const amount = Number(line.cost?.totalAmount?.amount);
      if (Number.isFinite(amount)) {
        totalDiscountAmount += (amount * pct) / 100;
        proofs.forEach((p) => {
          if (getDiscountForProof(p, variantId) > 0 || (sku && getDiscountForProof(p, sku) > 0))
            usedProofIds.add(p.proofId);
        });
      }
    }
  }
  appliedProofCount = usedProofIds.size;
  return { totalDiscountAmount, currencyCode, appliedProofCount };
}
