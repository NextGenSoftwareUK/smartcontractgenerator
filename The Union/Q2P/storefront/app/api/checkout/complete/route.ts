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

    // Initialize payment sessions first
    const paymentRes = await fetch(`${ENDPOINT}/store/payment-collections`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-publishable-api-key': MEDUSA_API_KEY,
      },
      body: JSON.stringify({ cart_id: cartId }),
    });

    if (!paymentRes.ok) {
      // Try completing directly if payment collection fails (manual/test mode)
      const completeRes = await fetch(`${ENDPOINT}/store/carts/${cartId}/complete`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-publishable-api-key': MEDUSA_API_KEY,
        },
      });
      const completeData = await completeRes.json();
      const res = NextResponse.json(completeData, { status: completeRes.status });
      if (completeData?.order?.id) {
        res.cookies.delete('cartId');
      }
      return res;
    }

    const paymentData = await paymentRes.json();
    const collectionId = paymentData?.payment_collection?.id;

    if (collectionId) {
      // Initialize payment sessions
      await fetch(`${ENDPOINT}/store/payment-collections/${collectionId}/payment-sessions`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-publishable-api-key': MEDUSA_API_KEY,
        },
      });
    }

    // Complete the cart / place order
    const completeRes = await fetch(`${ENDPOINT}/store/carts/${cartId}/complete`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-publishable-api-key': MEDUSA_API_KEY,
      },
    });

    const completeData = await completeRes.json();
    const response = NextResponse.json(completeData, { status: completeRes.status });

    if (completeData?.order?.id || completeData?.type === 'order') {
      response.cookies.delete('cartId');
    }

    return response;
  } catch (e) {
    return NextResponse.json({ error: String(e) }, { status: 500 });
  }
}
