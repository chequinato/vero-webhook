/**
 * VERO — marca de aferição.
 *
 * Não é um escudo nem um cadeado: é um punção de conferência, do tipo que se
 * carimba numa peça depois de medi-la. Um quadro fino (o campo de inspeção),
 * quatro marcas de registro nas laterais, um V que atravessa a base do quadro
 * — a peça foi verificada e o carimbo não cabe nela — e o fio horizontal da
 * leitura cruzando o meio.
 *
 * Na montagem: registro → quadro → V. Cerca de 700ms, uma vez só.
 */
export function Mark({ size = 26, animate = true }: { size?: number; animate?: boolean }) {
  const a = animate ? '' : ' mark--static';
  return (
    <svg
      className={`mark${a}`}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
      xmlns="http://www.w3.org/2000/svg"
    >
      {/* marcas de registro */}
      <g className="mark-ticks" stroke="currentColor" strokeWidth="1">
        <path d="M12 0.5V2.4" />
        <path d="M0.5 12H2.4" />
        <path d="M21.6 12H23.5" />
      </g>

      {/* campo de inspeção */}
      <rect className="mark-frame" x="3.5" y="3.5" width="17" height="17" stroke="currentColor" strokeWidth="1" />

      {/* fio de leitura */}
      <path className="mark-scan" d="M8.2 12.6H15.8" stroke="currentColor" strokeWidth="1" />

      {/* V — rompe a base do quadro */}
      <path
        className="mark-v"
        d="M7.4 6.6L12 23.2L16.6 6.6"
        stroke="currentColor"
        strokeWidth="2.9"
        strokeLinecap="butt"
        strokeLinejoin="miter"
      />
    </svg>
  );
}
