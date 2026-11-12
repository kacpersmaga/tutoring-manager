import { Audio } from "@remotion/media";
import { linearTiming, TransitionSeries } from "@remotion/transitions";
import { fade } from "@remotion/transitions/fade";
import React from "react";
import { AbsoluteFill, Sequence, staticFile, useVideoConfig } from "remotion";
import marks from "../public/clips/marks.json";
import voice from "../public/voice/durations.json";
import { color, desk } from "./theme";
import { Clip, Rise, Shot, Title } from "./scenes/parts";
import { PhoneScene } from "./scenes/PhoneScene";

type VoiceLine = keyof typeof voice;

const seconds = (ms: number) => ms / 1000;
const clipSeconds = (clip: { ready: number; end: number }) => seconds(clip.end - clip.ready);
const crossfade = 0.5;
const voiceDelay = 0.3;

/** A scene lasts its own length, or longer when its narration needs the time. */
const sceneSeconds = (base: number, line: VoiceLine) => Math.max(base, voiceDelay + voice[line] + 0.6);

const scenes: { line: VoiceLine; base: number }[] = [
  { line: "intro", base: 5 },
  { line: "attendance", base: 4 },
  { line: "notes", base: 3 },
  { line: "money", base: 3 },
  { line: "calendar", base: clipSeconds(marks.calendar) },
  { line: "email", base: clipSeconds(marks.email) },
  { line: "phone", base: clipSeconds(marks.phone) },
  { line: "coming", base: clipSeconds(marks.coming) },
  { line: "studentNotes", base: 4 },
  { line: "payments", base: clipSeconds(marks.payments) },
  { line: "balance", base: 4 },
  { line: "stack", base: 6 },
  { line: "outro", base: 6 },
];

export const demoFilmSeconds =
  scenes.reduce((total, scene) => total + sceneSeconds(scene.base, scene.line), 0) - (scenes.length - 1) * crossfade;

/** The narrated demo film: the problem, the product at work on real recordings, the stack, the link. */
export const DemoFilm: React.FC = () => {
  const { fps } = useVideoConfig();
  const frames = (line: VoiceLine, base: number) => Math.round(sceneSeconds(base, line) * fps);
  const cut = <TransitionSeries.Transition presentation={fade()} timing={linearTiming({ durationInFrames: Math.round(crossfade * fps) })} />;
  const narration = (line: VoiceLine) => (
    <Sequence from={Math.round(voiceDelay * fps)} name={`Voice: ${line}`}>
      <Audio src={staticFile(`voice/${line}.mp3`)} />
    </Sequence>
  );
  // A recording shorter than its narration plays slightly slower instead of freezing on a blank frame.
  const rate = (clip: { ready: number; end: number }, line: VoiceLine) => clipSeconds(clip) / sceneSeconds(clipSeconds(clip), line);

  return (
    <AbsoluteFill style={{ backgroundColor: color.desk }}>
      <TransitionSeries>
        <TransitionSeries.Sequence name="Intro" durationInFrames={frames("intro", 5)}>
          <Title kicker="Tutoring Manager" head="Every lesson confirmed. Every złoty matched." />
          {narration("intro")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Problem: attendance" durationInFrames={frames("attendance", 4)}>
          <Title kicker="A tutor’s evening, question one" head="Is Ola coming tomorrow?" />
          {narration("attendance")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Problem: notes" durationInFrames={frames("notes", 3)}>
          <Title kicker="Question two" head="What did we cover last time?" />
          {narration("notes")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Problem: money" durationInFrames={frames("money", 3)}>
          <Title kicker="Question three" head="Who paid for October?" />
          {narration("money")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Calendar" durationInFrames={frames("calendar", clipSeconds(marks.calendar))}>
          <Clip src="clips/calendar.webm" caption="Your week, with every answer on it" trimBefore={Math.round(seconds(marks.calendar.ready) * fps)} playbackRate={rate(marks.calendar, "calendar")} zoom={[1, 1.03]} />
          {narration("calendar")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Email" durationInFrames={frames("email", clipSeconds(marks.email))}>
          <Clip src="clips/email.webm" caption="24 hours before, one email with a link" trimBefore={Math.round(seconds(marks.email.ready) * fps)} playbackRate={rate(marks.email, "email")} zoom={[1, 1.05]} />
          {narration("email")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Phone" durationInFrames={frames("phone", clipSeconds(marks.phone))}>
          <PhoneScene trimBefore={Math.round(seconds(marks.phone.ready) * fps)} />
          {narration("phone")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Coming" durationInFrames={frames("coming", clipSeconds(marks.coming))}>
          <Clip src="clips/coming.webm" caption="“Coming” lands in your calendar" trimBefore={Math.round(seconds(marks.coming.ready) * fps)} playbackRate={rate(marks.coming, "coming")} zoom={[1, 1.06]} />
          {narration("coming")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Notes" durationInFrames={frames("studentNotes", 4)}>
          <Shot src="shots/student.jpg" caption="Notes that follow the student" width={2384} height={1718} />
          {narration("studentNotes")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Payments" durationInFrames={frames("payments", clipSeconds(marks.payments))}>
          <Clip src="clips/payments.webm" caption="Import the statement. Transfers find their students." trimBefore={Math.round(seconds(marks.payments.ready) * fps)} playbackRate={rate(marks.payments, "payments")} />
          {narration("payments")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Balance" durationInFrames={frames("balance", 4)}>
          <Shot src="shots/payments.jpg" caption="A running balance, not a guess" width={2384} height={1264} />
          {narration("balance")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Stack" durationInFrames={frames("stack", 6)}>
          <Title kicker="Under the hood" head="Built with .NET, PostgreSQL and React." line="Tested on a real database. Emails safe on several servers. Lessons stay put when the clocks change." />
          {narration("stack")}
        </TransitionSeries.Sequence>
        {cut}
        <TransitionSeries.Sequence name="Outro" durationInFrames={frames("outro", 6)}>
          <AbsoluteFill style={{ ...desk, justifyContent: "center", padding: "0 160px" }}>
            <Rise style={{ fontSize: 40, fontWeight: 500, color: color.muted, marginBottom: 28 }}>Run it in two minutes</Rise>
            <Rise delay={4} style={{ alignSelf: "flex-start", fontSize: 84, fontWeight: 600, letterSpacing: "-0.02em", padding: "28px 44px", borderRadius: 18, backgroundColor: color.board, color: color.boardType }}>
              docker compose up
            </Rise>
            <Rise delay={14} style={{ fontSize: 46, fontWeight: 500, color: color.ink2, marginTop: 48 }}>github.com/kacpersmaga/tutoring-manager</Rise>
            <Rise delay={20} style={{ position: "absolute", left: 160, bottom: 80, fontSize: 26, fontWeight: 500, color: color.muted }}>Narration: ElevenLabs</Rise>
          </AbsoluteFill>
          {narration("outro")}
        </TransitionSeries.Sequence>
      </TransitionSeries>
    </AbsoluteFill>
  );
};
