// src/utils/currency.js
// Single source of truth for money formatting - this app is South African (see Khoi Pro), so every
// amount is Rand, never a locale-default currency symbol.
export const formatCurrency = (amount, { decimals = 2 } = {}) => {
  const n = Number(amount) || 0;
  return `R${n.toLocaleString(undefined, { minimumFractionDigits: decimals, maximumFractionDigits: decimals })}`;
};
