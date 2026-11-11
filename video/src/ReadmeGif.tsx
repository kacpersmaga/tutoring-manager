import { Video } from "@remotion/media";
import { linearTiming, TransitionSeries } from "@remotion/transitions";
import { fade } from "@remotion/transitions/fade";
import React from "react";
import { AbsoluteFill, staticFile, useVideoConfig } from "remotion";
import marks from "../public/clips/marks.json";
import { color } from "./theme";

const seconds = (ms: number) => ms / 1000;

export const readmeGifSeconds =
  seconds(marks.calendar.dialog + 1800 - marks.calendar.ready - 1500) +
  seconds(marks.phone.end - marks.phone.ready - 600) +
  2 +
  seconds(marks.payments.assigned + 1200 - marks.payments.ready - 1000) -
  3 * 0.3;

/** About 15 seconds for the top of the README: lesson card, the student's tap, the card changes, the statement import. */
export const ReadmeGif: React.FC = () => {
  const { fps } = useVideoConfig();
  const cut = <TransitionSeries.Transition presentation={fade()} timing={linearTiming({ durationInFrames: Math.round(0.3 * fps) })} />;
  const clip = (name: string, from: number, to: number, phone = false) => (
    <AbsoluteFill style={{ backgroundColor: color.desk, alignItems: "center", justifyContent: "center" }}>
      <Video
        src={staticFile(`clips/${name}.webm`)}
        trimBefore={Math.round(seconds(from) * fps)}
        trimAfter={Math.round(seconds(to) * fps)}
        muted
        style={phone ? { height: "100%" } : { width: "100%", height: "100%" }}
      />
    </AbsoluteFill>
  );

  return (
    <TransitionSeries>
      <TransitionSeries.Sequence durationInFrames={Math.round(seconds(marks.calendar.dialog + 1800 - marks.calendar.ready - 1500) * fps)}>
        {clip("calendar", marks.calendar.ready + 1500, marks.calendar.dialog + 1800)}
      </TransitionSeries.Sequence>
      {cut}
      <TransitionSeries.Sequence durationInFrames={Math.round(seconds(marks.phone.end - marks.phone.ready - 600) * fps)}>
        {clip("phone", marks.phone.ready + 600, marks.phone.end, true)}
      </TransitionSeries.Sequence>
      {cut}
      <TransitionSeries.Sequence durationInFrames={Math.round(2 * fps)}>
        {clip("coming", marks.coming.ready, marks.coming.ready + 2000)}
      </TransitionSeries.Sequence>
      {cut}
      <TransitionSeries.Sequence durationInFrames={Math.round(seconds(marks.payments.assigned + 1200 - marks.payments.ready - 1000) * fps)}>
        {clip("payments", marks.payments.ready + 1000, marks.payments.assigned + 1200)}
      </TransitionSeries.Sequence>
    </TransitionSeries>
  );
};
