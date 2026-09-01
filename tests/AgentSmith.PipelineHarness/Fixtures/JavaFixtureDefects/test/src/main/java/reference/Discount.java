package reference;

/** Defect variant for the `test` stage: it compiles, and it divides by the wrong scale. */
public final class Discount {

    private Discount() {
    }

    public static long applyDiscount(long priceInCents, int percentOff) {
        if (percentOff < 0 || percentOff > 100) {
            throw new IllegalArgumentException("percentOff must be between 0 and 100");
        }
        return priceInCents * (100 - percentOff) / 10;
    }
}
