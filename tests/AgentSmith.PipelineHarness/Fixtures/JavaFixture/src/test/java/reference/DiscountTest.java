package reference;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import org.junit.jupiter.api.Test;

class DiscountTest {

    @Test
    void takesAPercentageOffThePrice() {
        assertEquals(15000L, Discount.applyDiscount(20000L, 25));
    }

    @Test
    void leavesAPriceUntouchedAtZeroPercent() {
        assertEquals(1999L, Discount.applyDiscount(1999L, 0));
    }

    @Test
    void refusesAPercentageOutsideTheRange() {
        assertThrows(IllegalArgumentException.class, () -> Discount.applyDiscount(1000L, 101));
    }
}
