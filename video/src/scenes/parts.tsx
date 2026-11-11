import { Video } from "@remotion/media";
import React from "react";
import { AbsoluteFill, CanvasImage, Easing, interpolate, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { color, desk, paperShadow } from "../theme";

const ease = Easing.bezier(0.16, 1, 0.3, 1);

/** Text that rises in over half a second. */
export const Rise: React.FC<{ delay?: number; children: React.ReactNode; style?: React.CSSProperties }> = ({ delay = 0, children, style }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  return (
    <div
      style={{
        ...style,
        opacity: interpolate(frame, [delay, delay + 0.5 * fps], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: ease }),
        translate: interpolate(frame, [delay, delay + 0.6 * fps], ["0px 28px", "0px 0px"], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: ease }),
      }}
    >
      {children}
    </div>
  );
};

/** One headline scene on the desk: an optional small line above and a big head. */
export const Title: React.FC<{ kicker?: string; head: string; line?: string }> = ({ kicker, head, line }) => (
  <AbsoluteFill style={{ ...desk, justifyContent: "center", padding: "0 160px" }}>
    {kicker && <Rise style={{ fontSize: 40, fontWeight: 500, color: color.muted, marginBottom: 28 }}>{kicker}</Rise>}
    <Rise delay={4} style={{ fontSize: 120, fontWeight: 600, letterSpacing: "-0.035em", lineHeight: 1.02, maxWidth: 1500 }}>{head}</Rise>
    {line && <Rise delay={14} style={{ fontSize: 46, fontWeight: 500, color: color.ink2, marginTop: 40, maxWidth: 1400, lineHeight: 1.3 }}>{line}</Rise>}
  </AbsoluteFill>
);

/** A screen recording laid on the desk as a sheet of paper, with its caption above. */
export const Clip: React.FC<{ src: string; caption: string; trimBefore: number; width?: number; height?: number; zoom?: [number, number] }> = ({
  src, caption, trimBefore, width = 1440, height = 900, zoom = [1, 1],
}) => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();
  const boxHeight = 800;
  const boxWidth = (width / height) * boxHeight;
  return (
    <AbsoluteFill style={{ ...desk, alignItems: "center" }}>
      <Rise style={{ marginTop: 70, fontSize: 64, fontWeight: 600, letterSpacing: "-0.03em" }}>{caption}</Rise>
      <div
        style={{
          marginTop: 44, width: boxWidth, height: boxHeight, borderRadius: 18, overflow: "hidden",
          backgroundColor: color.cotton, boxShadow: paperShadow,
          scale: interpolate(frame, [0, durationInFrames], zoom, { extrapolateRight: "clamp" }),
        }}
      >
        <Video src={staticFile(src)} trimBefore={trimBefore} muted style={{ width: "100%", height: "100%", objectFit: "cover" }} />
      </div>
    </AbsoluteFill>
  );
};

/** A still screenshot, slowly drifting, for screens the recording does not cover. */
export const Shot: React.FC<{ src: string; caption: string; width: number; height: number }> = ({ src, caption, width, height }) => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();
  const boxWidth = 1500;
  return (
    <AbsoluteFill style={{ ...desk, alignItems: "center" }}>
      <Rise style={{ marginTop: 70, fontSize: 64, fontWeight: 600, letterSpacing: "-0.03em" }}>{caption}</Rise>
      <div style={{ marginTop: 44, width: boxWidth, height: 800, borderRadius: 18, overflow: "hidden", backgroundColor: color.cotton, boxShadow: paperShadow }}>
        <CanvasImage
          src={staticFile(src)}
          width={boxWidth}
          height={(height / width) * boxWidth}
          style={{ translate: interpolate(frame, [0, durationInFrames], ["0px 0px", `0px ${-Math.max(0, (height / width) * boxWidth - 800)}px`], { easing: Easing.inOut(Easing.quad) }) }}
        />
      </div>
    </AbsoluteFill>
  );
};
