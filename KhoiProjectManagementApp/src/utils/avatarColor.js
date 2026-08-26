// Deterministic hue per person - the same name always resolves to the same swatch, so a given
// person's avatar looks the same everywhere they appear (sidebar, topbar, Team grid).
const PALETTE = ['bg-blue-600', 'bg-teal-600', 'bg-emerald-600', 'bg-orange-500', 'bg-fuchsia-700'];

export const getAvatarColor = (name) => {
  const str = name || '?';
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = (hash * 31 + str.charCodeAt(i)) | 0;
  }
  return PALETTE[Math.abs(hash) % PALETTE.length];
};
