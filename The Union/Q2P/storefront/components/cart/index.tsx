import { createCart, getCart } from 'lib/medusa';
import { cookies } from 'next/headers';
import CartModal from './modal';
import OpenCart from './open-cart';

export default async function Cart() {
  try {
    const cartId = cookies().get('cartId')?.value;
    let cart;

    if (cartId) {
      cart = await getCart(cartId);
    }

    // If the `cartId` from the cookie is not set or the cart is empty
    // (old carts becomes `null` when you checkout), then get a new `cartId`
    //  and re-fetch the cart.
    if (!cartId || !cart) {
      cart = await createCart();
    }

    return <CartModal cart={cart} />;
  } catch {
    // Medusa unavailable - show cart icon that does nothing
    return <OpenCart quantity={0} />;
  }
}
