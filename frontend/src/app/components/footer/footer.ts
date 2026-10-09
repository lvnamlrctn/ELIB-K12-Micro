import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './footer.html',
  host: {
    class: 'block w-full shrink-0 mt-auto z-10 relative'
  }
})
export class Footer {}
