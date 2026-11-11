import { loadFont } from "@remotion/fonts";

// The Coral palette from src/TutoringManager.Web/src/styles/tokens.css, so the film matches the product.
export const color = {
  paper: "oklch(97% 0.006 55)",
  paper2: "oklch(94.5% 0.007 55)",
  desk: "oklch(94.3% 0.008 64)",
  cotton: "oklch(98.8% 0.0045 80)",
  ink: "oklch(21% 0.012 38)",
  ink2: "oklch(29% 0.013 40)",
  muted: "oklch(47% 0.013 45)",
  board: "oklch(22% 0.011 40)",
  boardType: "oklch(96.5% 0.006 70)",
  paid: "oklch(55.5% 0.15 33)",
};

export const font = "General Sans";

// General Sans from Fontshare (free licence), loaded at render time.
loadFont({
  family: font,
  weight: "500",
  url: "https://cdn.fontshare.com/wf/3RZHWSNONLLWJK3RLPEKUZOMM56GO4LJ/BPDRY7AHVI3MCDXXVXTQQ76H3UXA63S3/SB2OEB6IKZPRR6JT4GFJ2TFT6HBB6AZN.woff2",
});
loadFont({
  family: font,
  weight: "600",
  url: "https://cdn.fontshare.com/wf/K46YRH762FH3QJ25IQM3VAXAKCHEXXW4/ISLWQPUZHZF33LRIOTBMFOJL57GBGQ4B/3ZLMEXZEQPLTEPMHTQDAUXP5ZZXCZAEN.woff2",
});

export const desk = {
  backgroundColor: color.desk,
  backgroundImage: `radial-gradient(90% 120% at 0% 30%, ${color.cotton}, transparent 62%)`,
  fontFamily: font,
  color: color.ink,
};

export const paperShadow =
  "inset 0 1px 0 oklch(100% 0.005 80 / 0.9), 0 0 0 0.5px oklch(32% 0.03 50 / 0.22), 18px 16px 34px -14px oklch(32% 0.03 50 / 0.35)";
