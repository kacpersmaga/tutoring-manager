import React from "react";
import { Composition } from "remotion";
import { DemoFilm, demoFilmSeconds } from "./DemoFilm";
import { ReadmeGif, readmeGifSeconds } from "./ReadmeGif";

export const RemotionRoot: React.FC = () => (
  <>
    <Composition id="DemoFilm" component={DemoFilm} fps={30} width={1920} height={1080} durationInFrames={Math.floor(30 * demoFilmSeconds)} />
    <Composition id="ReadmeGif" component={ReadmeGif} fps={15} width={1280} height={800} durationInFrames={Math.floor(15 * readmeGifSeconds)} />
  </>
);
