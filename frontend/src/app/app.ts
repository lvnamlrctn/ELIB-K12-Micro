import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {RouterOutlet} from '@angular/router';
import {TranslateService} from '@ngx-translate/core';
import { ToastrComponent } from './components/toastr/toastr';
import { UnsavedChangesDialogComponent } from './components/unsaved-changes-dialog/unsaved-changes-dialog';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-root',
  imports: [RouterOutlet, ToastrComponent, UnsavedChangesDialogComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private translate = inject(TranslateService);

  constructor() {
    this.translate.addLangs(['en', 'vi']);
    this.translate.setDefaultLang('vi');
    this.translate.use('vi');
  }
}
