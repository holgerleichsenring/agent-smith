import { describe, expect, it } from 'vitest';
import { applyDiscount } from './price';

describe('applyDiscount', () => {
  it('takes a percentage off the price', () => {
    expect(applyDiscount(200, 25)).toBe(150);
  });

  it('leaves a price untouched at zero percent', () => {
    expect(applyDiscount(19.99, 0)).toBe(19.99);
  });

  it('refuses a percentage outside 0..100', () => {
    expect(() => applyDiscount(10, 101)).toThrow(RangeError);
  });
});
