// src/components/Common/RandIcon.js
// lucide-react has no South African Rand icon (it covers DollarSign/Euro/PoundSterling/
// IndianRupee/etc. but not Rand) - the Rand's actual symbol is just the letter "R" (unlike $/€/£
// it has no distinct glyph), so this renders that letter in the same 24x24 viewBox/currentColor
// convention every lucide icon uses, so it drops in wherever a lucide icon component is expected
// (same className-driven sizing/color, e.g. `h-7 w-7 text-gray-700`).
import React from 'react';

const RandIcon = ({ className = '', ...props }) => (
  <svg
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 24 24"
    fill="none"
    className={className}
    {...props}
  >
    <text
      x="50%"
      y="50%"
      dy="0.35em"
      textAnchor="middle"
      fontSize="17"
      fontWeight="700"
      fill="currentColor"
      stroke="none"
    >
      R
    </text>
  </svg>
);

export default RandIcon;
