import { NextRequest, NextResponse } from 'next/server';

const ENDPOINT = process.env.NEXT_PUBLIC_MEDUSA_BACKEND_API ?? 'http://localhost:9000';
const MEDUSA_API_KEY = process.env.MEDUSA_API_KEY ?? '';

export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
  try {
    const cartId = request.cookies.get('cartId')?.value;
    if (!cartId) {
      return NextResponse.json({ error: 'No cart found' }, { status: 400 });
    }

    const body = await request.json();

    const res = await fetch(`${ENDPOINT}/store/carts/${cartId}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-publishable-api-key': MEDUSA_API_KEY,
      },
      body: JSON.stringify(body),
    });

    const data = await res.json();
    return NextResponse.json(data, { status: res.status });
  } catch (e) {
    return NextResponse.json({ error: String(e) }, { status: 500 });
  }
}
