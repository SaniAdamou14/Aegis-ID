# Aegis-ID — Product Backlog complet

**Auditeur de posture de sécurité pour Microsoft Entra ID**
Backlog produit — version 1.0 — 24 juillet 2026

---

## 0. Note sur le nom

`Aegis-ID` est proposé plutôt qu'un nom contenant « Entra » ou « Azure ». Microsoft
interdit l'usage de ses marques dans le nom d'un produit tiers, y compris open source.
Un dépôt nommé `EntraGuard` ou `AzureIAMScanner` t'expose à une demande de retrait, et
c'est exactement le genre de faux pas qu'un jury de cybersécurité relève. Le README peut
en revanche dire librement : *« Aegis-ID audits Microsoft Entra ID tenants »* — l'usage
descriptif est autorisé.

Alternatives si `Aegis-ID` est déjà pris sur GitHub : `IdPosture`, `Sentinel-IAM`,
`TenantAudit`.

---

## 1. Vision produit

> Pour les **administrateurs d'identité et les consultants sécurité** qui gèrent un tenant
> Microsoft Entra ID, **Aegis-ID** est un **outil d'audit en lecture seule** qui détecte
> les mauvaises configurations d'identité, les classe par sévérité et les rattache aux
> référentiels CIS Microsoft 365 Benchmark et MITRE ATT&CK.
> Contrairement à un audit manuel dans le portail Azure, Aegis-ID produit un rapport
> reproductible, versionnable et comparable dans le temps, en moins de deux minutes.

### Principe de conception non négociable

**Aegis-ID ne demande jamais un scope d'écriture Microsoft Graph.**
L'outil lit, corrèle et rapporte. Il ne modifie rien, ne supprime rien, ne crée rien.
Ce choix est un argument de vente, une exigence de sécurité et une simplification
d'architecture à la fois. Il figure en tête du README et du threat model.

---

## 2. Personas

| Persona | Rôle | Besoin principal | Contrainte |
|---|---|---|---|
| **Amina, IAM Administrator** | Administratrice Entra ID dans une PME de 400 employés | Savoir en une exécution ce qui est mal configuré, sans lire 200 pages de benchmark | N'est pas développeuse ; veut une interface et un PDF |
| **Karim, Consultant sécurité** | Auditeur externe, missions courtes chez plusieurs clients | Un rapport standardisé, exportable, avec traçabilité des références | Ne peut pas installer d'agent chez le client |
| **Lucie, Ingénieure DevSecOps** | Intègre les contrôles de sécurité en CI | Une CLI qui sort du JSON et un code retour non nul si findings critiques | Pas d'interaction humaine possible dans le pipeline |
| **Sani, Mainteneur** | Toi | Un projet démontrable, testable et lisible par un jury d'admission | 4 semaines, à temps partiel |

---

## 3. Épics

| ID | Épic | Objectif |
|---|---|---|
| **E1** | Authentification et connexion au tenant | Se connecter de façon sûre et en moindre privilège |
| **E2** | Collecte de données Graph | Récupérer de manière fiable et paginée l'état du tenant |
| **E3** | Moteur de contrôles | Évaluer des règles déclaratives et produire des findings |
| **E4** | Catalogue de contrôles | Implémenter les 15 contrôles de la v1 |
| **E5** | Restitution et export | CLI, JSON, CSV, PDF |
| **E6** | Tableau de bord Angular | Visualisation, filtrage, détail d'un finding |
| **E7** | Historique et comparaison | Suivre l'évolution de la posture entre deux scans |
| **E8** | Qualité, CI et documentation | Tests, pipeline, README, threat model |

---

## 4. User stories

Format : `En tant que <persona>, je veux <action> afin de <bénéfice>.`
Estimation en points Fibonacci. Priorité MoSCoW.

---

### E1 — Authentification et connexion au tenant

---

**US-001 — Authentification par application enregistrée**
**Priorité : Must** · **Points : 5**

> En tant que **Karim**, je veux authentifier Aegis-ID auprès d'un tenant client via une
> app registration dédiée (client credentials flow), afin de lancer un audit sans utiliser
> mon compte personnel.

**Critères d'acceptation**
- Étant donné un `tenantId`, un `clientId` et un `clientSecret` valides, quand je lance
  `aegis scan`, alors l'outil obtient un token Graph et affiche le nom du tenant audité.
- Étant donné un `clientSecret` invalide ou expiré, quand je lance le scan, alors l'outil
  affiche une erreur explicite mentionnant l'échec d'authentification, sans exposer le
  secret dans les logs, et retourne le code de sortie `2`.
- Étant donné un secret fourni en variable d'environnement `AEGIS_CLIENT_SECRET`, quand je
  lance le scan sans argument `--secret`, alors la variable est utilisée.
- Aucun secret n'apparaît jamais dans la sortie console, les fichiers de log ou les
  rapports exportés.

---

**US-002 — Authentification interactive par device code**
**Priorité : Should** · **Points : 3**

> En tant qu'**Amina**, je veux me connecter avec mon compte administrateur via un code
> affiché à l'écran, afin de tester l'outil sans créer d'app registration.

**Critères d'acceptation**
- Quand je lance `aegis scan --interactive`, alors un code et une URL sont affichés et
  l'outil attend la validation.
- Étant donné une authentification réussie, quand le scan démarre, alors les permissions
  effectives sont celles déléguées à mon compte.
- Étant donné un délai de plus de 15 minutes sans validation, alors l'outil abandonne avec
  un message clair.

---

**US-003 — Vérification préalable des permissions**
**Priorité : Must** · **Points : 3**

> En tant qu'**Amina**, je veux savoir avant le scan quelles permissions manquent, afin de
> ne pas découvrir des contrôles en échec après cinq minutes d'exécution.

**Critères d'acceptation**
- Quand je lance `aegis doctor`, alors l'outil liste chaque permission requise
  (`Directory.Read.All`, `Policy.Read.All`, `AuditLog.Read.All`, `Application.Read.All`,
  `RoleManagement.Read.Directory`, `UserAuthenticationMethod.Read.All`) avec le statut
  `accordée` ou `manquante`.
- Étant donné une permission manquante, alors la sortie indique quels contrôles seront
  ignorés et fournit la commande ou le lien de remédiation.
- Étant donné qu'une permission d'écriture serait accordée à l'application, alors l'outil
  affiche un avertissement : Aegis-ID n'en a pas besoin et recommande de la retirer.

---

### E2 — Collecte de données Graph

---

**US-004 — Collecte paginée et résiliente**
**Priorité : Must** · **Points : 8**

> En tant que **Lucie**, je veux que le scan aboutisse même sur un tenant de plusieurs
> milliers d'objets, afin de l'intégrer en CI sans surveillance.

**Critères d'acceptation**
- Étant donné une réponse Graph contenant `@odata.nextLink`, quand la collecte s'exécute,
  alors toutes les pages sont suivies jusqu'à épuisement.
- Étant donné une réponse HTTP `429` avec en-tête `Retry-After`, quand la collecte
  s'exécute, alors l'outil attend la durée indiquée et réessaie, jusqu'à 5 tentatives,
  avec backoff exponentiel et jitter.
- Étant donné une réponse `5xx`, alors le même mécanisme de retry s'applique.
- Étant donné un échec définitif sur un collecteur, alors les autres collecteurs
  poursuivent et le rapport signale la collecte partielle.
- Sur un tenant de 500 utilisateurs et 50 applications, le scan complet s'exécute en
  moins de 90 secondes.

---

**US-005 — Snapshot de tenant persistable**
**Priorité : Must** · **Points : 5**

> En tant que **Sani**, je veux que les données brutes collectées soient matérialisées en
> un objet `TenantSnapshot`, afin de pouvoir rejouer l'évaluation des contrôles hors ligne
> et écrire des tests déterministes.

**Critères d'acceptation**
- Le snapshot contient : utilisateurs, méthodes d'authentification, rôles annuaires et
  assignations, applications et service principals, politiques de Conditional Access,
  paramètres d'autorisation du tenant, journaux de connexion agrégés.
- Quand je lance `aegis scan --dump snapshot.json`, alors le snapshot complet est écrit
  sur disque.
- Quand je lance `aegis evaluate --from snapshot.json`, alors les contrôles sont évalués
  sans aucun appel réseau.
- Le snapshot exporté est pseudonymisable via `--redact` : UPN et noms d'affichage
  remplacés par des identifiants stables et non réversibles.

---

### E3 — Moteur de contrôles

---

**US-006 — Exécution du moteur de règles**
**Priorité : Must** · **Points : 8**

> En tant que **Sani**, je veux un moteur qui découvre et exécute les contrôles de manière
> uniforme, afin d'ajouter un nouveau contrôle sans toucher au noyau.

**Critères d'acceptation**
- Un contrôle implémente une interface unique exposant : identifiant, titre, sévérité,
  références (CIS, MITRE), permissions requises, et une méthode d'évaluation prenant un
  `TenantSnapshot` et retournant une liste de `Finding`.
- Les contrôles sont découverts automatiquement au démarrage ; ajouter une classe suffit.
- Étant donné un contrôle dont les permissions requises sont absentes, alors il est marqué
  `Skipped` avec motif, et non `Passed`.
- Étant donné une exception levée par un contrôle, alors le contrôle est marqué `Error`,
  l'exception est loggée, et les autres contrôles s'exécutent.
- Chaque contrôle est évaluable indépendamment via `aegis scan --control IAM-004`.

---

**US-007 — Modèle de finding et scoring**
**Priorité : Must** · **Points : 5**

> En tant qu'**Amina**, je veux que chaque problème détecté soit classé par sévérité et
> accompagné d'une remédiation, afin de savoir par quoi commencer lundi matin.

**Critères d'acceptation**
- Un `Finding` porte : identifiant du contrôle, sévérité (`Critical`, `High`, `Medium`,
  `Low`, `Info`), objet concerné (type + identifiant + nom lisible), preuve, description
  du risque, étapes de remédiation, références CIS et MITRE, horodatage.
- Un score de posture global sur 100 est calculé, pondéré par sévérité, et la formule est
  documentée dans le README.
- Étant donné un contrôle sans finding, alors il apparaît explicitement comme `Passed`
  dans le rapport — l'absence de finding doit être visible, pas silencieuse.

---

**US-008 — Suppressions et exceptions documentées**
**Priorité : Should** · **Points : 5**

> En tant que **Karim**, je veux pouvoir marquer un finding comme accepté avec une
> justification, afin qu'il n'apparaisse pas comme un défaut au rapport suivant.

**Critères d'acceptation**
- Un fichier `aegis-suppressions.yaml` permet de supprimer un finding par identifiant de
  contrôle et identifiant d'objet, avec un champ `reason` obligatoire et un champ
  `expires` optionnel.
- Une suppression sans `reason` fait échouer le chargement du fichier.
- Étant donné une suppression expirée, alors le finding réapparaît et un avertissement
  signale l'expiration.
- Les findings supprimés sont comptés séparément dans le rapport, jamais fondus dans les
  `Passed`.

---

### E4 — Catalogue de contrôles v1

Chaque contrôle ci-dessous est **une user story à part entière**, de format identique.
Priorité **Must** pour IAM-001 à IAM-010, **Should** pour IAM-011 à IAM-015.
Estimation : **3 points** chacun, sauf indication contraire.

| ID | Titre | Sévérité | Référence CIS M365 | MITRE ATT&CK |
|---|---|---|---|---|
| **IAM-001** | Comptes à rôle privilégié sans MFA enregistrée | Critical | 1.1.1 | T1078.004 |
| **IAM-002** | Rôles privilégiés assignés en permanence plutôt qu'éligibles via PIM | High | 1.1.3 | T1098.003 |
| **IAM-003** | Nombre de Global Administrators hors de la fourchette 2–4 | High | 1.1.1 | T1078.004 |
| **IAM-004** | Absence de comptes break-glass conformes (exclus des CA, MFA hardware) | High | 1.1.2 | T1078.004 |
| **IAM-005** | Secrets ou certificats d'application expirant sous 30 jours, ou sans expiration | High | 1.2.1 | T1552.001 |
| **IAM-006** | Applications détenant des permissions Graph à haut risque | Critical | 2.1.1 | T1098.001 |
| **IAM-007** | Consentement utilisateur aux applications non restreint | High | 5.1.5 | T1528 |
| **IAM-008** | Service principals sans authentification depuis plus de 90 jours | Medium | — | T1078.004 |
| **IAM-009** | Authentification héritée non bloquée par une politique CA active | Critical | 1.2.2 | T1110.003 |
| **IAM-010** | Politiques de Conditional Access en `report-only` ou désactivées | Medium | 1.2.x | T1562.001 |
| **IAM-011** | Exclusions permanentes d'utilisateurs ou groupes dans les politiques CA | High | 1.2.x | T1562.001 |
| **IAM-012** | Enregistrement d'applications autorisé à tous les utilisateurs | Medium | 5.1.2 | T1098.001 |
| **IAM-013** | Comptes invités porteurs d'un rôle annuaire | High | 5.1.6 | T1078.004 |
| **IAM-014** | Invitation d'invités autorisée à tous les membres | Medium | 5.1.6 | T1136.003 |
| **IAM-015** | Comptes utilisateurs actifs sans connexion depuis plus de 90 jours | Medium | 1.1.4 | T1078.004 |

**Gabarit de user story pour chaque contrôle** — exemple avec IAM-001 :

> En tant qu'**Amina**, je veux détecter les comptes à rôle privilégié qui n'ont aucune
> méthode MFA forte enregistrée, afin d'éliminer le vecteur de compromission le plus
> exploité contre les tenants Entra ID.

**Critères d'acceptation**
- Étant donné un utilisateur assigné à un rôle de la liste des rôles privilégiés
  (Global Administrator, Privileged Role Administrator, Security Administrator, Exchange
  Administrator, SharePoint Administrator, User Administrator, Application Administrator,
  Cloud Application Administrator, Authentication Administrator, Helpdesk Administrator),
  et dont les méthodes d'authentification ne contiennent ni FIDO2, ni Microsoft
  Authenticator, ni Windows Hello, ni certificat, quand le contrôle s'exécute, alors un
  finding `Critical` est produit pour cet utilisateur.
- Étant donné un utilisateur privilégié disposant uniquement d'une MFA par SMS ou appel
  vocal, alors un finding `High` est produit, avec mention explicite de la faiblesse des
  méthodes téléphoniques.
- Étant donné un compte break-glass identifié dans la configuration, alors il est évalué
  mais son finding porte la mention `expected exception`.
- Le finding cite l'UPN, la liste des rôles concernés et les méthodes actuellement
  enregistrées.
- Le contrôle est couvert par au moins trois tests unitaires sur snapshot figé : cas
  conforme, cas sans MFA, cas MFA faible.

---

### E5 — Restitution et export

---

**US-009 — Sortie console lisible**
**Priorité : Must** · **Points : 3**

> En tant qu'**Amina**, je veux une sortie console claire et hiérarchisée, afin de
> comprendre le résultat sans ouvrir de fichier.

**Critères d'acceptation**
- La sortie affiche : nom du tenant, horodatage, score de posture, décompte par sévérité,
  puis les findings groupés par contrôle et triés par sévérité décroissante.
- Les couleurs sont désactivées automatiquement si la sortie n'est pas un terminal, ou via
  `NO_COLOR`.
- `--quiet` réduit la sortie au score et aux décomptes.
- `--verbose` ajoute le détail des appels Graph et les durées par contrôle.

---

**US-010 — Export JSON et code de sortie CI**
**Priorité : Must** · **Points : 3**

> En tant que **Lucie**, je veux un export JSON stable et un code de sortie exploitable,
> afin de faire échouer un pipeline sur régression de posture.

**Critères d'acceptation**
- `--output json --file report.json` produit un JSON dont le schéma est versionné et
  publié dans le dépôt.
- `--fail-on Critical` retourne le code `1` si au moins un finding de cette sévérité ou
  supérieure existe, `0` sinon.
- Le code `2` est réservé aux erreurs d'exécution, distinct du code `1` des findings.
- Le JSON inclut la liste des contrôles `Skipped` et le motif.

---

**US-011 — Export CSV**
**Priorité : Should** · **Points : 2**

> En tant que **Karim**, je veux exporter les findings en CSV, afin de les intégrer au
> tableau de suivi de remédiation du client.

**Critères d'acceptation**
- `--output csv` produit un fichier à une ligne par finding, encodé UTF-8 avec BOM pour
  compatibilité Excel.
- Les colonnes sont : `ControlId`, `Severity`, `ObjectType`, `ObjectId`, `ObjectName`,
  `Evidence`, `Remediation`, `CisReference`, `MitreTechnique`, `DetectedAt`.
- Les champs contenant des virgules ou des sauts de ligne sont correctement échappés.

---

**US-012 — Rapport PDF d'audit**
**Priorité : Could** · **Points : 8**

> En tant que **Karim**, je veux générer un PDF présentable, afin de le remettre
> directement au client en fin de mission.

**Critères d'acceptation**
- Le PDF contient une page de garde, un résumé exécutif avec le score et un graphique de
  répartition par sévérité, puis le détail des findings, puis une annexe méthodologique.
- Le nom et le logo du cabinet sont paramétrables via un fichier de configuration.
- La génération d'un rapport de 200 findings prend moins de 10 secondes.

---

### E6 — Tableau de bord Angular

---

**US-013 — Vue d'ensemble de la posture**
**Priorité : Must** · **Points : 8**

> En tant qu'**Amina**, je veux une page d'accueil montrant l'état global du tenant, afin
> de saisir la situation en cinq secondes.

**Critères d'acceptation**
- La page affiche le score de posture, le nombre de findings par sévérité, le nombre de
  contrôles passés / échoués / ignorés, et la date du dernier scan.
- Cliquer sur un décompte de sévérité filtre la liste des findings.
- Un état vide explicite est affiché si aucun scan n'a encore été effectué.
- Un indicateur de chargement est visible pendant la récupération des données.

---

**US-014 — Liste des findings filtrable**
**Priorité : Must** · **Points : 5**

> En tant qu'**Amina**, je veux filtrer et trier les findings, afin de traiter d'abord ce
> qui concerne mon périmètre.

**Critères d'acceptation**
- Filtres disponibles : sévérité, contrôle, type d'objet, recherche texte libre sur le nom
  d'objet.
- Les filtres actifs sont reflétés dans l'URL et restaurés au rechargement de la page.
- La liste est paginée à 25 éléments et reste fluide sur 1 000 findings.

---

**US-015 — Détail d'un finding**
**Priorité : Must** · **Points : 3**

> En tant qu'**Amina**, je veux ouvrir un finding et voir la remédiation complète, afin de
> corriger sans chercher ailleurs.

**Critères d'acceptation**
- Le panneau de détail affiche la preuve brute, la description du risque, les étapes de
  remédiation numérotées, et les liens sortants vers la documentation Microsoft, la
  référence CIS et la technique MITRE.
- Un bouton copie la preuve JSON dans le presse-papiers.

---

**US-016 — Accessibilité et responsive**
**Priorité : Should** · **Points : 3**

> En tant qu'**Amina**, je veux consulter le tableau de bord depuis un écran de portable
> ou une tablette, afin de le montrer en réunion.

**Critères d'acceptation**
- Toutes les fonctions sont accessibles au clavier seul.
- Les contrastes respectent WCAG 2.1 niveau AA.
- La mise en page reste utilisable à partir de 768 px de large.
- La sévérité n'est jamais signalée par la couleur seule : une icône ou un libellé
  l'accompagne.

---

### E7 — Historique et comparaison

---

**US-017 — Persistance des scans**
**Priorité : Should** · **Points : 5**

> En tant qu'**Amina**, je veux que chaque scan soit conservé, afin de démontrer une
> amélioration à ma direction.

**Critères d'acceptation**
- Chaque exécution est enregistrée en base (SQLite par défaut, PostgreSQL en option) avec
  son score, ses décomptes et ses findings.
- La rétention est paramétrable, par défaut 90 jours.
- Les données persistées ne contiennent aucun secret ni token.

---

**US-018 — Comparaison de deux scans**
**Priorité : Should** · **Points : 5**

> En tant que **Lucie**, je veux comparer le scan courant au précédent, afin d'identifier
> les régressions introduites par un changement de configuration.

**Critères d'acceptation**
- `aegis diff --against <scanId>` affiche les findings apparus, résolus et inchangés.
- La commande retourne le code `1` si au moins un nouveau finding `Critical` ou `High`
  est apparu.
- Le tableau de bord affiche une courbe du score sur les 10 derniers scans.

---

### E8 — Qualité, CI et documentation

---

**US-019 — Suite de tests sur snapshots figés**
**Priorité : Must** · **Points : 8**

> En tant que **Sani**, je veux tester chaque contrôle sur des snapshots synthétiques,
> afin de garantir la non-régression sans dépendre d'un tenant réel.

**Critères d'acceptation**
- Un jeu de snapshots JSON de test couvre, pour chaque contrôle, au moins un cas conforme
  et un cas non conforme.
- Les tests s'exécutent sans réseau et sans identifiants.
- La couverture de la couche domaine dépasse 80 %.
- Un test vérifie qu'aucun scope d'écriture n'est présent dans la liste des permissions
  demandées — ce test est le garde-fou du principe de conception du projet.

---

**US-020 — Pipeline d'intégration continue**
**Priorité : Must** · **Points : 5**

> En tant que **Sani**, je veux une CI verte et visible, afin qu'un visiteur du dépôt voie
> immédiatement que le projet est sérieux.

**Critères d'acceptation**
- La CI GitHub Actions exécute build, tests, analyse statique et scan de secrets
  (`gitleaks`) sur chaque push et pull request.
- Les badges de build et de couverture figurent en haut du README.
- Une CI en échec bloque la fusion.
- Une release taguée publie automatiquement les binaires Linux, Windows et macOS, plus une
  image conteneur.

---

**US-021 — Documentation d'accueil**
**Priorité : Must** · **Points : 5**

> En tant que **visiteur du dépôt**, je veux comprendre en deux minutes ce que fait le
> projet et comment l'essayer, afin de décider s'il mérite mon attention.

**Critères d'acceptation**
- Le README anglais contient, dans cet ordre : une phrase de description, un GIF de démo,
  la liste des 15 contrôles, l'installation, un quickstart en trois commandes, les
  permissions requises, l'architecture, et les limites connues.
- Un fichier `docs/threat-model.md` décrit les actifs, les acteurs, les hypothèses de
  confiance, ce que l'outil détecte et ce qu'il ne détecte pas.
- Un fichier `docs/controls/IAM-XXX.md` par contrôle détaille la logique, les faux
  positifs connus et les références.
- `SECURITY.md`, `LICENSE` (Apache 2.0) et `CONTRIBUTING.md` sont présents.
- Un diagramme d'architecture Mermaid est versionné dans le dépôt.

---

**US-022 — Environnement de démonstration reproductible**
**Priorité : Should** · **Points : 5**

> En tant que **jury ou recruteur**, je veux essayer l'outil sans posséder de tenant Entra
> ID, afin d'évaluer le travail immédiatement.

**Critères d'acceptation**
- `aegis demo` charge un snapshot fictif embarqué et produit un rapport complet, sans
  aucune connexion réseau.
- Le snapshot de démonstration déclenche au moins un finding par contrôle.
- Une commande `docker compose up` lance l'API et le tableau de bord préchargés avec les
  données de démonstration.

---

## 5. Exigences non fonctionnelles

| ID | Exigence | Critère mesurable |
|---|---|---|
| **NFR-01** | Moindre privilège | Seuls des scopes `.Read` sont demandés ; vérifié par test automatisé |
| **NFR-02** | Performance | Tenant de 500 utilisateurs et 50 apps audité en moins de 90 s |
| **NFR-03** | Résilience | Gestion du throttling `429` avec backoff exponentiel et jitter |
| **NFR-04** | Confidentialité | Aucun secret en log, en base ou en rapport ; option `--redact` |
| **NFR-05** | Portabilité | Binaire autonome Linux, Windows, macOS ; image conteneur |
| **NFR-06** | Traçabilité | Chaque finding référence une source CIS et une technique MITRE |
| **NFR-07** | Déterminisme | Deux évaluations d'un même snapshot produisent un résultat identique |
| **NFR-08** | Journalisation | Logs structurés, niveau paramétrable, aucune donnée personnelle en niveau `Info` |

---

## 6. Hors périmètre v1

À écrire explicitement dans le README — délimiter le périmètre est un signe de maturité,
pas un aveu de faiblesse.

- Toute action de remédiation automatique (choix de conception, pas une limite technique)
- Audit des charges de travail Microsoft 365 : Exchange, SharePoint, Teams
- Audit Azure RBAC au niveau abonnement et ressources
- Surveillance en temps réel ou alerting
- Multi-tenant simultané dans une même exécution
- Gestion des utilisateurs et des rôles au sein d'Aegis-ID lui-même

---

## 7. Découpage en 4 sprints d'une semaine

| Sprint | Objectif de sprint | Stories | Points |
|---|---|---|---|
| **S1** | Un scan authentifié qui produit des findings pour 5 contrôles | US-001, US-003, US-004, US-005, US-006, US-007, IAM-001, IAM-005, IAM-006, IAM-009, IAM-003 | 49 |
| **S2** | Catalogue complet et sorties exploitables | IAM-002, IAM-004, IAM-007, IAM-008, IAM-010 à IAM-015, US-009, US-010, US-011 | 41 |
| **S3** | Tableau de bord Angular fonctionnel | US-013, US-014, US-015, US-016, US-017 | 24 |
| **S4** | Qualité, documentation, démonstration, publication | US-019, US-020, US-021, US-022, US-002, US-008 | 31 |

**Total v1 : 145 points.**

`US-012` (PDF) et `US-018` (diff) sont volontairement hors des quatre sprints. Ce sont tes
réserves : si S1 déborde — et S1 déborde presque toujours — tu les sacrifies sans toucher
au périmètre annoncé. Si tout va bien, elles deviennent la v1.1 en septembre, ce qui donne
au dépôt une activité continue au moment où les jurys le consulteront.

---

## 8. Definition of Ready

Une story entre en sprint si :
- le persona, l'action et le bénéfice sont explicites ;
- les critères d'acceptation sont écrits et testables ;
- les dépendances techniques sont identifiées ;
- les permissions Graph nécessaires sont connues ;
- l'estimation est posée.

## 9. Definition of Done

Une story est terminée si :
- le code est fusionné sur `main` via pull request ;
- les tests unitaires associés passent et la couverture ne régresse pas ;
- la CI est verte, `gitleaks` inclus ;
- la documentation utilisateur est à jour, y compris la fiche de contrôle si applicable ;
- le comportement est vérifié manuellement sur le tenant développeur ;
- aucun `TODO` ni code commenté ne subsiste dans le diff.

---

## 10. Prérequis à mettre en place avant le sprint 1

1. Créer un **Microsoft 365 Developer Program tenant** (gratuit, 25 licences, renouvelable).
2. Le peupler : 30 à 50 utilisateurs fictifs, 3 comptes privilégiés dont un sans MFA,
   quelques applications avec des permissions volontairement excessives, deux politiques
   de Conditional Access dont une en `report-only`, deux comptes invités.
   Ce tenant volontairement mal configuré est ton banc d'essai — scripte son
   provisionnement et versionne le script, c'est un atout de plus dans le dépôt.
3. Créer l'app registration Aegis-ID avec les six permissions applicatives en lecture.
4. Initialiser le dépôt : squelette .NET 8, `gitleaks` en pre-commit, `LICENSE`,
   `.gitignore` incluant `*.env`, `appsettings.Local.json` et `*.snapshot.json`.
