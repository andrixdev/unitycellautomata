using UnityEngine;
using System.Collections;

public class LifeSim : MonoBehaviour {
	private int DEAD = 0;
	private int ALIVE = 1;
	
	public readonly int nbrOfCells;
	public float cellLength;
	public bool torusShape;
	
	private Cell[][][] cellGrid;
	public int[][][] nextStatusGrid; 
	
	private float timer;
	public float pauseDuration;
	
	//Test key : 1 097 364 208 640
	public Int64 ruleKey;
	private Int64 oldRuleKey = 0;
	private int[][] rule;
	
	public void Start(){
		// Init rule array (26x2)
		int[] r = new int[2] { DEAD, DEAD };
		rule = new int[][] { r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r };
	}
	
	private void translateRule(){ //Transforme la chaine ruleKey en une rule utilisable
		//Il y a 26*2 cas (de 0 à 26 voisins vivants, selon son propre status) qui doivent être couverts par une règle
		// Il faut donc 52 bits pour encoder une règle
		
		//Lecture de la règle pour la cellule initailement morte
		for(int i = 0;i<26;i++){
			this.rule[i][0] = ((ruleKey & (1<<i)) == 0) ? DEAD : ALIVE;
		}
		
		//Cellule initiallement vivante 
		for(int i = 26;i<56;i++){
			this.rule[i][1] = ((ruleKey & (1<<i)) == 0) ? DEAD : ALIVE;
		}
	}
	
	private int countNeighbours(int x, int y, int z){ //Compte le nombre de voisins vivants
		int key = 0;
		for(int i = -1;i<=1;i++){
			for(int j = -1;j<=1;j++){
				for(int k = -1;k<=1;k++){
					if (this.torusShape){
						if (!(i==0 && j==0 && k==0)){ //Plusieurs opérations modulo pour assurer un résultat positif
							key += this.cellGrid[((x+i)%this.nbrOfCells+this.nbrOfCells)%nbrOfCells][((y+j)%this.nbrOfCells+this.nbrOfCells)%nbrOfCells][((z+k)%this.nbrOfCells+this.nbrOfCells)%nbrOfCells].getStatus();
						}
					}else{
						if((x+i>=0 && x+i<this.nbrOfCells)&&(y+j>=0 && y+j<this.nbrOfCells)&&(z+k>=0 && z+k<this.nbrOfCells) 
							&& !(i==0 && j==0 && k==0)){
							key += this.cellGrid[x+i][y+j][z+k].getStatus();
						}
					}
					
				}
			}
		}
		return key;
	}
	
	private void buildNextGrid(){
		for(int x = 0;x<this.nbrOfCells;x++){
			for(int y = 0;y<this.nbrOfCells;y++){
				for(int z = 0;z<this.nbrOfCells;z++){
					this.nextStatusGrid[x][y][z] = this.rule[this.countNeighbours(x,y,z)][this.cellGrid[x][y][z].getStatus()];
				}
			}
		}
	}
	
	private void nextStep(){
		for(int x = 0;x<this.nbrOfCells;x++){
			for(int y = 0;y<this.nbrOfCells;y++){
				for(int z = 0;z<this.nbrOfCells;z++){
					this.cellGrid[x][y][z].changeStatus(nextStatusGrid[x][y][z]);
				}
			}
		}
	}
	
}
