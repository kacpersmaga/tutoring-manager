import { linearTiming, TransitionSeries } from "@remotion/transitions";
import { fade } from "@remotion/transitions/fade";
import React from "react";
import { AbsoluteFill, useVideoConfig } from "remotion";
import marks from "../public/clips/marks.json";
import { color, desk } from "./theme";
import { Clip, Rise, Shot, Title } from "./scenes/parts";
import { PhoneScene } from "./scenes/PhoneScene";

const seconds = (ms: number) => ms / 1000;
const clipSeconds = (clip: { ready: number; end: number }) => seconds(clip.end - clip.ready);

/** Fixed scenes plus the recorded clips, minus the 12 half-second crossfades. */
export const demoFilmSeconds =
  6 + 4 + 4 + 4 + 5 + 5 + 6 + 7 +
  clipSeconds(marks.calendar) + clipSeconds(marks.email) + clipSeconds(marks.phone) + clipSeconds(marks.coming) + clipSeconds(marks.payments) -
  12 * 0.5;

/** The 60 to 90 second demo film: the problem, the product at work on real recordings, the stack, the link. */
export const DemoFilm: React.FC = () => {
  const { fps } = useVideoConfig();
  const cut = <TransitionSeries.Transition presentation={fade()} timing={linearTiming({ durationInFrames: Math.round(0.5 * fps) })} />;

  return (
    <AbsoluteFill style={{ backgroundColor: color.desk }}>
      <TransitionSeries>
        <TransitionSeries.Sequence name="Intro" durationInFrames={6 * fps}>
          <Title kicker="Tutoring Manager" head="Every lesson confirmed. Every złoty matched." />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Problem: attendance" durationInFrames={4 * fps}>
          <Title kicker="A tutor’s evening, question one" head="Is Ola coming tomorrow?" />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Problem: notes" durationInFrames={4 * fps}>
          <Title kicker="Question two" head="What did we cover last time?" />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Problem: money" durationInFrames={4 * fps}>
          <Title kicker="Question three" head="Who paid for October?" />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Calendar" durationInFrames={Math.round(seconds(marks.calendar.end - marks.calendar.ready) * fps)}>
          <Clip src="clips/calendar.webm" caption="Your week, with every answer on it" trimBefore={Math.round(seconds(marks.calendar.ready) * fps)} zoom={[1, 1.03]} />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Email" durationInFrames={Math.round(seconds(marks.email.end - marks.email.ready) * fps)}>
          <Clip src="clips/email.webm" caption="24 hours before, one email with a link" trimBefore={Math.round(seconds(marks.email.ready) * fps)} zoom={[1, 1.05]} />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Phone" durationInFrames={Math.round(seconds(marks.phone.end - marks.phone.ready) * fps)}>
          <PhoneScene trimBefore={Math.round(seconds(marks.phone.ready) * fps)} />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Coming" durationInFrames={Math.round(seconds(marks.coming.end - marks.coming.ready) * fps)}>
          <Clip src="clips/coming.webm" caption="“Coming” lands in your calendar" trimBefore={Math.round(seconds(marks.coming.ready) * fps)} zoom={[1, 1.06]} />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Notes" durationInFrames={5 * fps}>
          <Shot src="shots/student.jpg" caption="Notes that follow the student" width={2384} height={1718} />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Payments" durationInFrames={Math.round(seconds(marks.payments.end - marks.payments.ready) * fps)}>
          <Clip src="clips/payments.webm" caption="Import the statement. Transfers find their students." trimBefore={Math.round(seconds(marks.payments.ready) * fps)} />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Balance" durationInFrames={5 * fps}>
          <Shot src="shots/payments.jpg" caption="A running balance, not a guess" width={2384} height={1264} />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Stack" durationInFrames={6 * fps}>
          <Title kicker="Under the hood" head="Built with .NET, PostgreSQL and React." line="Tested on a real database. Emails safe on several servers. Lessons stay put when the clocks change." />
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Outro" durationInFrames={7 * fps}>
          <AbsoluteFill style={{ ...desk, justifyContent: "center", padding: "0 160px" }}>
            <Rise style={{ fontSize: 40, fontWeight: 500, color: color.muted, marginBottom: 28 }}>Run it in two minutes</Rise>
            <Rise delay={4} style={{ alignSelf: "flex-start", fontSize: 84, fontWeight: 600, letterSpacing: "-0.02em", padding: "28px 44px", borderRadius: 18, backgroundColor: color.board, color: color.boardType }}>
              docker compose up
            </Rise>
            <Rise delay={14} style={{ fontSize: 46, fontWeight: 500, color: color.ink2, marginTop: 48 }}>github.com/kacpersmaga/tutoring-manager</Rise>
          </AbsoluteFill>
        </TransitionSeries.Sequence>
      </TransitionSeries>
    </AbsoluteFill>
  );
};
