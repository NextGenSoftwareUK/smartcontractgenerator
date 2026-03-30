'use client';

import { getEntitlements, getOasisToken } from 'lib/oasis';
import Link from 'next/link';
import { useEffect, useState } from 'react';

/** Shows a message on checkout when the user has quest reward entitlements (for Q2P discount). */
export default function CheckoutQuestBanner() {
  const [hasEntitlements, setHasEntitlements] = useState(false);

  useEffect(() => {
    const token = getOasisToken();
    if (!token) return;
    getEntitlements(token)
      .then((list) => setHasEntitlements(list.some((p) => !p.redeemed && p.physicalReward)))
      .catch(() => setHasEntitlements(false));
  }, []);

  if (!hasEntitlements) return null;

  return (
    <div className="mb-6 rounded-lg border border-green-200 bg-green-50 p-4 text-sm text-green-800 dark:border-green-800 dark:bg-green-950/40 dark:text-green-200">
      <p className="font-medium">Quest reward discount</p>
      <p className="mt-1">
        You have completed quests that unlock a discount. Your discount is applied in the cart and
        will be reflected at payment.
      </p>
      <Link
        href="/"
        className="mt-2 inline-block text-green-700 underline dark:text-green-300"
      >
        View cart
      </Link>
    </div>
  );
}
