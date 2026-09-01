package reference;

/** Defect variant for the `build` stage: the discount is returned as the wrong type. */
public final class Discount {

    private Discount() {
    }

    public static long applyDiscount(long priceInCents, int percentOff) {
        if (percentOff < 0 || percentOff > 100) {
            throw new IllegalArgumentException("percentOff must be between 0 and 100");
        }
        String discounted = priceInCents * (100 - percentOff) / 100;
        return discounted;
    }
}
