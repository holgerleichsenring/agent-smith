/** Defect variant for the `test` stage: it compiles and lints, and it divides by the wrong scale. */
export function applyDiscount(price: number, percentOff: number): number {
  if (percentOff < 0 || percentOff > 100) {
    throw new RangeError('percentOff must be between 0 and 100');
  }
  return Math.round(price * (100 - percentOff)) / 10;
}
