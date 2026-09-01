/** Defect variant for the `build` stage: the discount is assigned to the wrong type. */
export function applyDiscount(price: number, percentOff: number): number {
  if (percentOff < 0 || percentOff > 100) {
    throw new RangeError('percentOff must be between 0 and 100');
  }
  const discounted: string = Math.round(price * (100 - percentOff)) / 100;
  return discounted;
}
