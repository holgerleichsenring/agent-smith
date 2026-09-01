/** The rule this reference repository exists to hold: a percentage off a price. */
export function applyDiscount(price: number, percentOff: number): number {
  if (percentOff < 0 || percentOff > 100) {
    throw new RangeError('percentOff must be between 0 and 100');
  }
  return Math.round(price * (100 - percentOff)) / 100;
}
