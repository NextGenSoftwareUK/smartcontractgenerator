export default function LogoIcon(props: React.ComponentProps<'svg'>) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      aria-label="The Union logo"
      viewBox="0 0 32 32"
      {...props}
    >
      {/* Tiger-stripe U mark */}
      <path
        d="M4 4 L4 20 Q4 28 16 28 Q28 28 28 20 L28 4 L22 4 L22 19 Q22 23 16 23 Q10 23 10 19 L10 4 Z"
        fill="currentColor"
      />
      {/* Stripe accents */}
      <rect x="4" y="4" width="4" height="10" fill="currentColor" opacity="0.5" />
      <rect x="24" y="4" width="4" height="10" fill="currentColor" opacity="0.5" />
    </svg>
  );
}
