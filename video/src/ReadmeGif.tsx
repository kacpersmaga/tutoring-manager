import { Video } from "@remotion/media";
import { linearTiming, TransitionSeries } from "@remotion/transitions";
import { fade } from "@remotion/transitions/fade";
import React from "react";
import { AbsoluteFill, staticFile, useVideoConfig } from "remotion";
import marks from "../public/clips/marks.json";
import { color, desk, paperShadow } from "./theme";

const seconds = (ms: number) => ms / 1000;

// Which part of each recording the GIF plays, in milliseconds from the clip's start.
const parts = {
  calendar: [marks.calendar.ready + 800, marks.calendar.dialog + 2200],
  phone: [marks.phone.ready + 400, marks.phone.end],
  coming: [marks.coming.ready, marks.coming.ready + 2400],
  payments: [marks.payments.ready, marks.payments.end],
} as const;

const crossfade = 0.3;

export const readmeGifSeconds =
  Object.values(parts).reduce((total, [from, to]) => total + seconds(to - from), 0) - 3 * crossfade;

/** A desktop recording as a sheet of paper on the desk. */
const DesktopFrame: React.FC<{ name: string; from: number; to: number }> = ({ name, from, to }) => {
  const { fps } = useVideoConfig();
  return (
    <AbsoluteFill style={{ ...desk, alignItems: "center", justifyContent: "center" }}>
      <div style={{ width: 1120, height: 700, borderRadius: 14, overflow: "hidden", backgroundColor: color.cotton, boxShadow: paperShadow }}>
        <Video src={staticFile(`clips/${name}.webm`)} trimBefore={Math.round(seconds(from) * fps)} trimAfter={Math.round(seconds(to) * fps)} muted style={{ width: "100%", height: "100%" }} />
      </div>
    </AbsoluteFill>
  );
};

/** The student's phone: a dark body with rounded corners and a speaker slot around the recording. */
const PhoneFrame: React.FC<{ from: number; to: number }> = ({ from, to }) => {
  const { fps } = useVideoConfig();
  return (
    <AbsoluteFill style={{ ...desk, alignItems: "center", justifyContent: "center" }}>
      <div style={{ position: "relative", width: 360, height: 760, padding: 14, borderRadius: 54, backgroundColor: color.board, boxShadow: paperShadow }}>
        <div style={{ width: "100%", height: "100%", borderRadius: 42, overflow: "hidden", backgroundColor: color.cotton }}>
          <Video src={staticFile("clips/phone.webm")} trimBefore={Math.round(seconds(from) * fps)} trimAfter={Math.round(seconds(to) * fps)} muted style={{ width: "100%", height: "100%", objectFit: "cover" }} />
        </div>
        <div style={{ position: "absolute", top: 26, left: "50%", width: 96, height: 26, marginLeft: -48, borderRadius: 13, backgroundColor: color.board }} />
      </div>
    </AbsoluteFill>
  );
};

/** About 20 seconds for the top of the README: the week, the student's tap, the answer arriving, a statement filling the month. */
export const ReadmeGif: React.FC = () => {
  const { fps } = useVideoConfig();
  const cut = <TransitionSeries.Transition presentation={fade()} timing={linearTiming({ durationInFrames: Math.round(crossfade * fps) })} />;
  const length = ([from, to]: readonly [number, number]) => Math.round(seconds(to - from) * fps);

  return (
    <TransitionSeries>
      <TransitionSeries.Sequence name="Week" durationInFrames={length(parts.calendar)}>
        <DesktopFrame name="calendar" from={parts.calendar[0]} to={parts.calendar[1]} />
      </TransitionSeries.Sequence>
      {cut}
      <TransitionSeries.Sequence name="Phone" durationInFrames={length(parts.phone)}>
        <PhoneFrame from={parts.phone[0]} to={parts.phone[1]} />
      </TransitionSeries.Sequence>
      {cut}
      <TransitionSeries.Sequence name="Coming" durationInFrames={length(parts.coming)}>
        <DesktopFrame name="coming" from={parts.coming[0]} to={parts.coming[1]} />
      </TransitionSeries.Sequence>
      {cut}
      <TransitionSeries.Sequence name="Payments" durationInFrames={length(parts.payments)}>
        <DesktopFrame name="payments" from={parts.payments[0]} to={parts.payments[1]} />
      </TransitionSeries.Sequence>
    </TransitionSeries>
  );
};
