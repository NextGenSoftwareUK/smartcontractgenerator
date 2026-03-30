import { createCart, getCart } from 'lib/medusa';
import { NextRequest, NextResponse } from 'next/server';

export const dynamic = 'force-dynamic';

export async function GET(request: NextRequest) {
  try {
    const cartId = request.cookies.get('cartId')?.value;
    let cart = null;

    if (cartId) {
      cart = await getCart(cartId);
    }
    if (!cartId || !cart) {
      cart = await createCart();
    }

    const res = NextResponse.json({ cart });
    if ((!cartId || !cart) && cart?.id) {
      res.cookies.set('cartId', cart.id, { path: '/', httpOnly: true, sameSite: 'lax' });
    }
    return res;
  } catch {
    return NextResponse.json({ cart: null });
  }
}
