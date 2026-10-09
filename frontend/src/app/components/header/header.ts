import { Component, inject, signal } from '@angular/core';
import { Auth } from '../../services/auth';
import { LayoutService } from '../../services/layout';
import { Router, RouterLink } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { PermissionService } from '../../services/system/permission.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './header.html',
  host: {
    class: 'block shrink-0 z-20 relative'
  }
})
export class Header {
  layout = inject(LayoutService);
  private auth = inject(Auth);
  private router = inject(Router);
  private translate = inject(TranslateService);
  private permission = inject(PermissionService);

  currentLang = signal(this.translate.currentLang?.toUpperCase() || 'VI');
  currentFlag = signal(this.getFlagUrl(this.translate.currentLang));

  user = this.auth.getUser();

  private getFlagUrl(lang: string | undefined): string {
    if (lang === 'en') {
      return 'https://upload.wikimedia.org/wikipedia/commons/a/a4/Flag_of_the_United_States.svg';
    }
    return 'https://upload.wikimedia.org/wikipedia/commons/2/21/Flag_of_Vietnam.svg';
  }

  setLang(code: string, flag: string) {
    const lang = code.toLowerCase();
    this.translate.use(lang);
    this.currentLang.set(code);
    this.currentFlag.set(flag);
    this.layout.langOpen.set(false);
  }

  logout() {
    this.auth.logout();
    this.permission.clear();
    this.router.navigate(['/admin/login']);
  }
}
