import { Video } from "@remotion/media";
import React from "react";
import { AbsoluteFill, staticFile } from "remotion";
import { color, desk, paperShadow } from "../theme";
import { Rise } from "./parts";

/** The student's side: the attendance page on a phone-sized sheet, with the caption beside it. */
export const PhoneScene: React.FC<{ trimBefore: number }> = ({ trimBefore }) => (
  <AbsoluteFill style={{ ...desk, flexDirection: "row", alignItems: "center", justifyContent: "center", gap: 140 }}>
    <div style={{ width: 640 }}>
      <Rise style={{ fontSize: 40, fontWeight: 500, color: color.muted, marginBottom: 24 }}>What the student sees</Rise>
      <Rise delay={4} style={{ fontSize: 84, fontWeight: 600, letterSpacing: "-0.035em", lineHeight: 1.04 }}>One tap. No account, no app.</Rise>
    </div>
    <div style={{ width: 430, height: 930, borderRadius: 48, overflow: "hidden", backgroundColor: color.cotton, boxShadow: paperShadow, rotate: "2deg" }}>
      <Video src={staticFile("clips/phone.webm")} trimBefore={trimBefore} muted style={{ width: "100%", height: "100%", objectFit: "cover" }} />
    </div>
  </AbsoluteFill>
);
