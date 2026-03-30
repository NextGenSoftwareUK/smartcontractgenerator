import { NextRequest, NextResponse } from 'next/server';

const ENDPOINT = process.env.NEXT_PUBLIC_MEDUSA_BACKEND_API ?? 'http://localhost:9000';
const MEDUSA_API_KEY = process.env.MEDUSA_API_KEY ?? '';

export const dynamic = 'force-dynamic';

export async function GET(request: NextRequest) {
  try {
    const cartId = request.cookies.get('cartId')?.value;
    if (!cartId) {
      return NextResponse.json({ shipping_options: [] });
    }

    const res = await fetch(
      `${ENDPOINT}/store/shipping-options?cart_id=${cartId}`,
      {
        headers: { 'x-publishable-api-key': MEDUSA_API_KEY },
        cache: 'no-store',
      }
    );

    const data = await res.json();
    return NextResponse.json(data, { status: res.status });
  } catch (e) {
    return NextResponse.json({ shipping_options: [], error: String(e) });
  }
}
