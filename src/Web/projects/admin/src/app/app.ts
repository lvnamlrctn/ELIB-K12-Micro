import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastrComponent } from './shared/toastr';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastrComponent],
  template: '<router-outlet /><app-toastr />',
})
export class App {}
