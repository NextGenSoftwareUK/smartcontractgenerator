'use client';

import LogoSquare from 'components/logo-square';
import Link from 'next/link';
import { useEffect, useState } from 'react';

// ─── Types ───────────────────────────────────────────────────────────────────

type CartLine = {
  id: string;
  title: string;
  quantity: number;
  thumbnail?: string;
  variant_title?: string;
  unit_price?: number;
  total?: number;
};

type CartSummary = {
  id: string;
  lines: CartLine[];
  subtotal?: number;
  total?: number;
  currency_code?: string;
};

type ShippingOption = {
  id: string;
  name: string;
  amount: number;
  currency_code?: string;
};

type AddressForm = {
  email: string;
  first_name: string;
  last_name: string;
  address_1: string;
  address_2: string;
  city: string;
  province: string;
  postal_code: string;
  country_code: string;
  phone: string;
};

type Step = 'address' | 'shipping' | 'payment' | 'confirmed';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function fmt(amount: number | undefined, currency = 'USD') {
  if (!amount && amount !== 0) return '—';
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency.toUpperCase(),
    minimumFractionDigits: 2,
  }).format(amount / 100);
}

const COUNTRIES = [
  { code: 'mx', name: 'Mexico' },
  { code: 'us', name: 'United States' },
  { code: 'gb', name: 'United Kingdom' },
  { code: 'de', name: 'Germany' },
  { code: 'fr', name: 'France' },
  { code: 'es', name: 'Spain' },
  { code: 'it', name: 'Italy' },
  { code: 'ca', name: 'Canada' },
  { code: 'au', name: 'Australia' },
  { code: 'dk', name: 'Denmark' },
  { code: 'se', name: 'Sweden' },
  { code: 'co', name: 'Colombia' },
];

// ─── Sub-components ───────────────────────────────────────────────────────────

function StepIndicator({ current }: { current: Step }) {
  const steps: { key: Step; label: string }[] = [
    { key: 'address', label: 'Address' },
    { key: 'shipping', label: 'Shipping' },
    { key: 'payment', label: 'Payment' },
  ];
  const idx = { address: 0, shipping: 1, payment: 2, confirmed: 3 };
  const currentIdx = idx[current];

  return (
    <div className="flex items-center gap-0 mb-10">
      {steps.map((s, i) => (
        <div key={s.key} className="flex items-center">
          <div className="flex flex-col items-center">
            <div
              className={`flex h-7 w-7 items-center justify-center rounded-full border text-xs font-display tracking-wider transition-colors ${
                i < currentIdx
                  ? 'border-union-gold bg-union-gold text-union-black'
                  : i === currentIdx
                  ? 'border-union-gold text-union-gold'
                  : 'border-union-muted/30 text-union-muted/50'
              }`}
            >
              {i < currentIdx ? '✓' : i + 1}
            </div>
            <span
              className={`mt-1 text-xs font-display tracking-widest ${
                i <= currentIdx ? 'text-union-gold' : 'text-union-muted/40'
              }`}
            >
              {s.label.toUpperCase()}
            </span>
          </div>
          {i < steps.length - 1 && (
            <div
              className={`mx-3 h-px w-12 transition-colors ${
                i < currentIdx ? 'bg-union-gold' : 'bg-union-muted/20'
              }`}
            />
          )}
        </div>
      ))}
    </div>
  );
}

function InputField({
  label,
  name,
  value,
  onChange,
  type = 'text',
  placeholder = '',
  required = false,
  half = false,
}: {
  label: string;
  name: string;
  value: string;
  onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  type?: string;
  placeholder?: string;
  required?: boolean;
  half?: boolean;
}) {
  return (
    <div className={half ? 'col-span-1' : 'col-span-2'}>
      <label className="mb-1 block text-xs font-display tracking-widest text-union-muted">
        {label.toUpperCase()}{required && <span className="ml-1 text-union-gold">*</span>}
      </label>
      <input
        type={type}
        name={name}
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        required={required}
        className="w-full rounded border border-union-gold/20 bg-union-black px-3 py-2.5 text-sm text-union-text placeholder-union-muted/40 outline-none transition-colors focus:border-union-gold/60"
      />
    </div>
  );
}

function SelectField({
  label,
  name,
  value,
  onChange,
  options,
  required = false,
}: {
  label: string;
  name: string;
  value: string;
  onChange: (e: React.ChangeEvent<HTMLSelectElement>) => void;
  options: { code: string; name: string }[];
  required?: boolean;
}) {
  return (
    <div className="col-span-2">
      <label className="mb-1 block text-xs font-display tracking-widest text-union-muted">
        {label.toUpperCase()}{required && <span className="ml-1 text-union-gold">*</span>}
      </label>
      <select
        name={name}
        value={value}
        onChange={onChange}
        required={required}
        className="w-full rounded border border-union-gold/20 bg-union-black px-3 py-2.5 text-sm text-union-text outline-none transition-colors focus:border-union-gold/60"
      >
        <option value="">Select country…</option>
        {options.map((o) => (
          <option key={o.code} value={o.code}>
            {o.name}
          </option>
        ))}
      </select>
    </div>
  );
}

function OrderSummary({ cart }: { cart: CartSummary | null }) {
  if (!cart) return null;
  const currency = cart.currency_code ?? 'usd';
  return (
    <div className="rounded-xl border border-union-gold/20 bg-union-panel p-5">
      <h3 className="mb-4 font-display tracking-widest text-union-gold text-sm">ORDER SUMMARY</h3>
      <ul className="space-y-3 text-sm">
        {cart.lines?.map((line) => (
          <li key={line.id} className="flex items-start gap-3">
            {line.thumbnail && (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={line.thumbnail}
                alt={line.title}
                className="h-12 w-12 rounded object-cover border border-union-gold/10"
              />
            )}
            <div className="flex-1 min-w-0">
              <p className="text-union-text truncate">{line.title}</p>
              {line.variant_title && line.variant_title !== 'Default Variant' && (
                <p className="text-xs text-union-muted">{line.variant_title}</p>
              )}
              <p className="text-xs text-union-muted">Qty: {line.quantity}</p>
            </div>
            <p className="text-union-gold text-xs whitespace-nowrap">
              {fmt(line.total, currency)}
            </p>
          </li>
        ))}
      </ul>
      <div className="mt-4 border-t border-union-gold/10 pt-4 space-y-1 text-sm">
        <div className="flex justify-between text-union-muted">
          <span>Subtotal</span>
          <span>{fmt(cart.subtotal, currency)}</span>
        </div>
        <div className="flex justify-between text-union-muted">
          <span>Shipping</span>
          <span className="text-xs">Calculated at next step</span>
        </div>
        <div className="flex justify-between font-semibold text-union-text pt-2 border-t border-union-gold/10">
          <span>Total</span>
          <span className="text-union-gold">{fmt(cart.total, currency)}</span>
        </div>
      </div>
    </div>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function CheckoutPage() {
  const [step, setStep] = useState<Step>('address');
  const [cart, setCart] = useState<CartSummary | null>(null);
  const [shippingOptions, setShippingOptions] = useState<ShippingOption[]>([]);
  const [selectedShipping, setSelectedShipping] = useState('');
  const [orderId, setOrderId] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const [address, setAddress] = useState<AddressForm>({
    email: '',
    first_name: '',
    last_name: '',
    address_1: '',
    address_2: '',
    city: '',
    province: '',
    postal_code: '',
    country_code: 'mx',
    phone: '',
  });

  // Load cart summary from Medusa
  useEffect(() => {
    fetch('/api/cart')
      .then((r) => r.json())
      .then((d) => {
        if (d?.cart) {
          const c = d.cart;
          setCart({
            id: c.id,
            lines: (c.items ?? []).map((item: any) => ({
              id: item.id,
              title: item.title ?? item.variant?.product?.title ?? '',
              quantity: item.quantity,
              thumbnail: item.thumbnail ?? item.variant?.product?.thumbnail,
              variant_title: item.description ?? item.variant?.title,
              unit_price: item.unit_price,
              total: item.total,
            })),
            subtotal: c.subtotal,
            total: c.total,
            currency_code: c.region?.currency_code ?? 'usd',
          });
        }
      })
      .catch(() => {});
  }, []);

  function handleAddressChange(e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) {
    setAddress((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  }

  async function submitAddress(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      const res = await fetch('/api/checkout/address', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: address.email,
          shipping_address: {
            first_name: address.first_name,
            last_name: address.last_name,
            address_1: address.address_1,
            address_2: address.address_2 || undefined,
            city: address.city,
            province: address.province || undefined,
            postal_code: address.postal_code,
            country_code: address.country_code,
            phone: address.phone || undefined,
          },
          billing_address: {
            first_name: address.first_name,
            last_name: address.last_name,
            address_1: address.address_1,
            city: address.city,
            country_code: address.country_code,
            postal_code: address.postal_code,
          },
        }),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data?.message ?? 'Could not save address. Please try again.');
        return;
      }
      // Fetch shipping options
      const soRes = await fetch('/api/checkout/shipping-options');
      const soData = await soRes.json();
      const options: ShippingOption[] = (soData?.shipping_options ?? []).map((o: any) => ({
        id: o.id,
        name: o.name,
        amount: o.amount ?? o.price_incl_tax ?? 0,
        currency_code: o.region?.currency_code ?? cart?.currency_code ?? 'usd',
      }));
      setShippingOptions(options);
      if (options.length === 1) setSelectedShipping(options[0]!.id);
      setStep('shipping');
    } catch {
      setError('Network error. Please try again.');
    } finally {
      setLoading(false);
    }
  }

  async function submitShipping(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedShipping && shippingOptions.length > 0) {
      setError('Please select a shipping method.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      if (selectedShipping) {
        const res = await fetch('/api/checkout/shipping-method', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ option_id: selectedShipping }),
        });
        if (!res.ok) {
          const d = await res.json();
          setError(d?.message ?? 'Could not set shipping method.');
          return;
        }
      }
      setStep('payment');
    } catch {
      setError('Network error. Please try again.');
    } finally {
      setLoading(false);
    }
  }

  async function placeOrder() {
    setLoading(true);
    setError('');
    try {
      const res = await fetch('/api/checkout/complete', { method: 'POST' });
      const data = await res.json();
      const order = data?.order ?? data;
      if (order?.id) {
        setOrderId(order.id);
        setStep('confirmed');
      } else if (data?.payment_collection) {
        // Payment provider needs action (e.g. Stripe redirect)
        setError('Payment requires additional action. Please contact us at jeff.sanchez91@gmail.com to complete your order.');
      } else {
        setError(data?.message ?? 'Could not place order. Please try again or contact us.');
      }
    } catch {
      setError('Network error. Please try again.');
    } finally {
      setLoading(false);
    }
  }

  // ─── Confirmed ──────────────────────────────────────────────────────────────
  if (step === 'confirmed') {
    return (
      <div className="min-h-screen bg-union-black px-4 py-16">
        <div className="mx-auto max-w-lg text-center">
          <div className="mb-6 text-5xl">🐯</div>
          <h1 className="font-display tracking-[0.2em] text-3xl text-union-gold mb-3">
            ORDER CONFIRMED
          </h1>
          <p className="text-union-muted text-sm mb-2">
            Thank you, {address.first_name}. Your order has been placed.
          </p>
          {orderId && (
            <p className="text-xs text-union-muted/60 mb-8 font-mono">
              Order ID: {orderId}
            </p>
          )}
          <p className="text-union-muted text-sm mb-10">
            A confirmation will be sent to <span className="text-union-text">{address.email}</span>.
            We&apos;ll reach out when your gear is on its way.
          </p>
          <div className="flex flex-col gap-3 items-center">
            <Link
              href="/"
              className="rounded border border-union-gold/40 px-8 py-2.5 font-display tracking-widest text-sm text-union-gold transition-colors hover:bg-union-gold hover:text-union-black"
            >
              BACK TO STORE
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // ─── Main layout ────────────────────────────────────────────────────────────
  return (
    <div className="min-h-screen bg-union-black">
      {/* Header */}
      <div className="border-b border-union-gold/20 bg-union-panel px-4 py-3">
        <div className="mx-auto flex max-w-5xl items-center justify-between">
          <Link href="/" className="flex items-center gap-3">
            <LogoSquare size="sm" />
            <span className="font-display tracking-[0.2em] text-union-gold text-sm">THE UNION</span>
          </Link>
          <span className="font-display tracking-widest text-union-muted text-xs">CHECKOUT</span>
        </div>
      </div>

      <div className="mx-auto max-w-5xl px-4 py-10">
        <div className="grid grid-cols-1 gap-10 lg:grid-cols-[1fr_360px]">
          {/* Left: form */}
          <div>
            <StepIndicator current={step} />

            {error && (
              <div className="mb-6 rounded border border-union-red/40 bg-union-red/10 px-4 py-3 text-sm text-red-300">
                {error}
              </div>
            )}

            {/* ── Step 1: Address ── */}
            {step === 'address' && (
              <form onSubmit={submitAddress}>
                <h2 className="mb-6 font-display tracking-[0.18em] text-xl text-union-text">
                  CONTACT & SHIPPING
                </h2>
                <div className="grid grid-cols-2 gap-4">
                  <div className="col-span-2">
                    <InputField
                      label="Email"
                      name="email"
                      type="email"
                      value={address.email}
                      onChange={handleAddressChange}
                      required
                      placeholder="you@example.com"
                    />
                  </div>
                  <InputField
                    label="First Name"
                    name="first_name"
                    value={address.first_name}
                    onChange={handleAddressChange}
                    required
                    half
                    placeholder="Mike"
                  />
                  <InputField
                    label="Last Name"
                    name="last_name"
                    value={address.last_name}
                    onChange={handleAddressChange}
                    required
                    half
                    placeholder="Garcia"
                  />
                  <InputField
                    label="Phone"
                    name="phone"
                    type="tel"
                    value={address.phone}
                    onChange={handleAddressChange}
                    half
                    placeholder="+52 984 000 0000"
                  />
                  <div className="col-span-1" />
                  <InputField
                    label="Address"
                    name="address_1"
                    value={address.address_1}
                    onChange={handleAddressChange}
                    required
                    placeholder="Calle 12 Norte 123"
                  />
                  <InputField
                    label="Apartment, suite, etc."
                    name="address_2"
                    value={address.address_2}
                    onChange={handleAddressChange}
                    placeholder="Optional"
                  />
                  <InputField
                    label="City"
                    name="city"
                    value={address.city}
                    onChange={handleAddressChange}
                    required
                    half
                    placeholder="Playa del Carmen"
                  />
                  <InputField
                    label="State / Province"
                    name="province"
                    value={address.province}
                    onChange={handleAddressChange}
                    half
                    placeholder="Quintana Roo"
                  />
                  <InputField
                    label="Postal Code"
                    name="postal_code"
                    value={address.postal_code}
                    onChange={handleAddressChange}
                    required
                    half
                    placeholder="77710"
                  />
                  <SelectField
                    label="Country"
                    name="country_code"
                    value={address.country_code}
                    onChange={handleAddressChange}
                    options={COUNTRIES}
                    required
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="mt-8 w-full rounded bg-union-gold py-3 font-display tracking-widest text-sm text-union-black transition-opacity hover:opacity-90 disabled:opacity-50"
                >
                  {loading ? 'SAVING…' : 'CONTINUE TO SHIPPING →'}
                </button>
              </form>
            )}

            {/* ── Step 2: Shipping ── */}
            {step === 'shipping' && (
              <form onSubmit={submitShipping}>
                <div className="mb-6 flex items-center justify-between">
                  <h2 className="font-display tracking-[0.18em] text-xl text-union-text">
                    SHIPPING METHOD
                  </h2>
                  <button
                    type="button"
                    onClick={() => { setStep('address'); setError(''); }}
                    className="text-xs text-union-muted underline-offset-2 hover:underline"
                  >
                    ← Edit address
                  </button>
                </div>

                {/* Address recap */}
                <div className="mb-6 rounded border border-union-gold/10 bg-union-panel px-4 py-3 text-sm text-union-muted">
                  <p>{address.first_name} {address.last_name} · {address.email}</p>
                  <p>{address.address_1}, {address.city}, {address.country_code.toUpperCase()}</p>
                </div>

                {shippingOptions.length === 0 ? (
                  <div className="rounded border border-union-gold/20 bg-union-panel p-6 text-center">
                    <p className="text-union-muted text-sm">
                      No shipping options available for this address yet.
                    </p>
                    <p className="mt-2 text-xs text-union-muted/60">
                      Contact us at{' '}
                      <a href="mailto:jeff.sanchez91@gmail.com" className="text-union-gold underline">
                        jeff.sanchez91@gmail.com
                      </a>{' '}
                      to arrange delivery.
                    </p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {shippingOptions.map((opt) => (
                      <label
                        key={opt.id}
                        className={`flex cursor-pointer items-center justify-between rounded border px-4 py-4 transition-colors ${
                          selectedShipping === opt.id
                            ? 'border-union-gold bg-union-gold/5'
                            : 'border-union-gold/20 hover:border-union-gold/40'
                        }`}
                      >
                        <div className="flex items-center gap-3">
                          <div
                            className={`h-4 w-4 rounded-full border-2 transition-colors ${
                              selectedShipping === opt.id
                                ? 'border-union-gold bg-union-gold'
                                : 'border-union-muted/40'
                            }`}
                          />
                          <span className="text-sm text-union-text">{opt.name}</span>
                        </div>
                        <span className="text-sm text-union-gold">
                          {opt.amount === 0 ? 'FREE' : fmt(opt.amount, opt.currency_code ?? 'usd')}
                        </span>
                        <input
                          type="radio"
                          className="sr-only"
                          name="shipping"
                          value={opt.id}
                          checked={selectedShipping === opt.id}
                          onChange={() => setSelectedShipping(opt.id)}
                        />
                      </label>
                    ))}
                  </div>
                )}

                <button
                  type="submit"
                  disabled={loading || (shippingOptions.length > 0 && !selectedShipping)}
                  className="mt-8 w-full rounded bg-union-gold py-3 font-display tracking-widest text-sm text-union-black transition-opacity hover:opacity-90 disabled:opacity-50"
                >
                  {loading ? 'SAVING…' : 'CONTINUE TO PAYMENT →'}
                </button>
              </form>
            )}

            {/* ── Step 3: Payment ── */}
            {step === 'payment' && (
              <div>
                <div className="mb-6 flex items-center justify-between">
                  <h2 className="font-display tracking-[0.18em] text-xl text-union-text">
                    PAYMENT
                  </h2>
                  <button
                    type="button"
                    onClick={() => { setStep('shipping'); setError(''); }}
                    className="text-xs text-union-muted underline-offset-2 hover:underline"
                  >
                    ← Edit shipping
                  </button>
                </div>

                {/* Address + shipping recap */}
                <div className="mb-6 space-y-2 rounded border border-union-gold/10 bg-union-panel px-4 py-3 text-sm text-union-muted">
                  <p>{address.first_name} {address.last_name} · {address.email}</p>
                  <p>{address.address_1}, {address.city}, {address.country_code.toUpperCase()}</p>
                  {selectedShipping && shippingOptions.length > 0 && (
                    <p>
                      {shippingOptions.find((o) => o.id === selectedShipping)?.name ?? 'Standard shipping'}
                    </p>
                  )}
                </div>

                <div className="rounded border border-union-gold/20 bg-union-panel px-6 py-5 mb-6">
                  <p className="font-display tracking-widest text-union-gold text-sm mb-3">
                    PAYMENT METHOD
                  </p>
                  <p className="text-sm text-union-muted mb-1">
                    Stripe payments are processed securely. By placing your order you confirm your details above are correct.
                  </p>
                  <p className="text-xs text-union-muted/60">
                    If payment requires further action, our team will contact you at {address.email}.
                  </p>
                </div>

                <button
                  onClick={placeOrder}
                  disabled={loading}
                  className="w-full rounded bg-union-gold py-3.5 font-display tracking-widest text-sm text-union-black transition-opacity hover:opacity-90 disabled:opacity-50"
                >
                  {loading ? 'PLACING ORDER…' : '🐯  PLACE ORDER'}
                </button>

                <p className="mt-4 text-center text-xs text-union-muted/50">
                  By placing your order you agree to The Union&apos;s terms. Questions?{' '}
                  <a href="mailto:jeff.sanchez91@gmail.com" className="text-union-gold underline">
                    Contact us
                  </a>
                </p>
              </div>
            )}
          </div>

          {/* Right: order summary */}
          <div className="order-first lg:order-last">
            <OrderSummary cart={cart} />
          </div>
        </div>
      </div>
    </div>
  );
}
