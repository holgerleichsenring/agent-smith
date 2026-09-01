import { Component } from '@angular/core';
import { applyDiscount } from './price';

@Component({
  selector: 'app-root',
  standalone: true,
  template: '<p>{{ discounted }}</p>',
})
export class App {
  readonly discounted: number = applyDiscount(200, 25);
}
