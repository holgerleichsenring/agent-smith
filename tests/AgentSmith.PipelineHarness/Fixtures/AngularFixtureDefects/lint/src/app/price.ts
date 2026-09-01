/** Defect variant for the `lint` stage: it compiles and its tests pass, but it types `any`. */
export function applyDiscount(price: number, percentOff: number): number {
  if (percentOff < 0 || percentOff > 100) {
    throw new RangeError('percentOff must be between 0 and 100');
  }
  const remaining: any = 100 - percentOff;
  return Math.round(price * remaining) / 100;
}
